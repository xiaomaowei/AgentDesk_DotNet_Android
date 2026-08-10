import org.gradle.api.tasks.Exec
import org.gradle.api.tasks.Sync

plugins {
    id("com.android.application")
}

// ---------------------------------------------------------------------------
// Versioning & VersionCode computation logic:
// Derives versionName from root VERSION or Gradle property VERSION_NAME.
// Derives versionCode from numeric SemVer X.Y.Z or Gradle property VERSION_CODE.
//
// Component bounds for numeric SemVer:
// - MAJOR: 0 .. 214747
// - MINOR: 0 .. 99
// - PATCH: 0 .. 99
// Monotonicity guarantee:
//   versionCode = MAJOR * 10000 + MINOR * 100 + PATCH
//   Since MINOR < 100 and PATCH < 100, any increase in (MAJOR, MINOR, PATCH)
//   guarantees a strictly monotonic increase in versionCode within 1 .. 2,147,483,647 (Android Int range).
// ---------------------------------------------------------------------------
val defaultVersionFile = rootDir.parentFile.resolve("VERSION")
val rawVersionName = providers.gradleProperty("VERSION_NAME").orNull
    ?: if (defaultVersionFile.exists()) defaultVersionFile.readText().trim() else "0.1.0"
val appVersionName = rawVersionName.removePrefix("v").trim()

fun computeVersionCode(ver: String): Int {
    val versionProp = providers.gradleProperty("VERSION_CODE").orNull
    if (!versionProp.isNullOrEmpty()) {
        val code = versionProp.toIntOrNull()
        require(code != null && code >= 1) {
            "Invalid VERSION_CODE property override: '$versionProp'. Must be a valid positive integer >= 1 within Android Int range."
        }
        return code
    }

    val semverRegex = Regex("""^(\d+)\.(\d+)\.(\d+)(?:-.*)?$""")
    val match = semverRegex.matchEntire(ver)
        ?: throw IllegalArgumentException("Invalid SemVer string '$ver' for versionCode computation.")

    val (majorStr, minorStr, patchStr) = match.destructured
    val major = majorStr.toIntOrNull()
        ?: throw IllegalArgumentException("SemVer major component '$majorStr' exceeds integer range.")
    val minor = minorStr.toIntOrNull()
        ?: throw IllegalArgumentException("SemVer minor component '$minorStr' exceeds integer range.")
    val patch = patchStr.toIntOrNull()
        ?: throw IllegalArgumentException("SemVer patch component '$patchStr' exceeds integer range.")

    require(minor in 0..99) {
        "SemVer minor component ($minor) out of bounds. Must be between 0 and 99."
    }
    require(patch in 0..99) {
        "SemVer patch component ($patch) out of bounds. Must be between 0 and 99."
    }
    require(major in 0..214747) {
        "SemVer major component ($major) out of bounds. Must be between 0 and 214747."
    }

    val calculatedCode = major * 10000 + minor * 100 + patch
    require(calculatedCode >= 1) {
        "Computed versionCode ($calculatedCode) must be >= 1."
    }

    return calculatedCode
}

val appVersionCode = computeVersionCode(appVersionName)

// ---------------------------------------------------------------------------
// Release Signing logic:
// Reads ANDROID_KEYSTORE_PATH, ANDROID_KEYSTORE_PASSWORD,
// ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD from Gradle properties.
// Environment variables ORG_GRADLE_PROJECT_* set these Gradle properties in CI.
// ---------------------------------------------------------------------------
val targetKeystorePath = providers.gradleProperty("ANDROID_KEYSTORE_PATH").orNull
val targetKeystorePassword = providers.gradleProperty("ANDROID_KEYSTORE_PASSWORD").orNull
val targetKeyAlias = providers.gradleProperty("ANDROID_KEY_ALIAS").orNull
val targetKeyPassword = providers.gradleProperty("ANDROID_KEY_PASSWORD").orNull

val resolvedKeystoreFile = if (!targetKeystorePath.isNullOrEmpty()) file(targetKeystorePath) else null

val hasReleaseSigning = resolvedKeystoreFile != null &&
                        resolvedKeystoreFile.exists() &&
                        !targetKeystorePassword.isNullOrEmpty() &&
                        !targetKeyAlias.isNullOrEmpty() &&
                        !targetKeyPassword.isNullOrEmpty()

// ---------------------------------------------------------------------------
// Web asset sync: syncs web-ui/dist → app/build/generated/web-assets/assets/
// so WebViewAssetLoader can serve them from the generated asset directory,
// automatically removing any stale/obsolete hashed asset bundles.
// ---------------------------------------------------------------------------

val webUiDir = rootDir.parentFile.resolve("web-ui")
val webDistDir = webUiDir.resolve("dist")
val generatedAssetsRoot = layout.buildDirectory.dir("generated/web-assets/assets").get().asFile
val isWindows = System.getProperty("os.name").lowercase().contains("windows")

val npmBuildWebUi by tasks.registering(Exec::class) {
    description = "Runs 'npm run build' inside web-ui."
    group = "build"

    workingDir(webUiDir)
    if (isWindows) {
        commandLine("cmd", "/c", "npm", "run", "build")
    } else {
        commandLine("npm", "run", "build")
    }

    // Source and config inputs for incremental up-to-date checking
    inputs.dir(webUiDir.resolve("src"))
    inputs.file(webUiDir.resolve("package.json"))
    val lockFile = webUiDir.resolve("package-lock.json")
    if (lockFile.exists()) {
        inputs.file(lockFile)
    }
    inputs.file(webUiDir.resolve("vite.config.ts"))
    inputs.file(webUiDir.resolve("tsconfig.json"))
    val tsconfigNode = webUiDir.resolve("tsconfig.node.json")
    if (tsconfigNode.exists()) {
        inputs.file(tsconfigNode)
    }
    val eslintConfig = webUiDir.resolve("eslint.config.js")
    if (eslintConfig.exists()) {
        inputs.file(eslintConfig)
    }
    inputs.file(webUiDir.resolve("index.html"))

    // Output: the dist directory
    outputs.dir(webDistDir)
}

val syncWebAssets by tasks.registering(Sync::class) {
    description = "Syncs web-ui/dist into the generated assets directory for the Android build, removing stale files."
    group = "build"

    dependsOn(npmBuildWebUi)

    from(webDistDir)
    into(generatedAssetsRoot)
}

android {
    namespace = "com.agentdeck.mobile"
    compileSdk = 37

    defaultConfig {
        applicationId = "com.agentdeck.mobile"
        minSdk = 26
        targetSdk = 37
        versionCode = appVersionCode
        versionName = appVersionName

        testInstrumentationRunner = "android.test.InstrumentationTestRunner"
    }

    signingConfigs {
        if (hasReleaseSigning) {
            create("release") {
                storeFile = resolvedKeystoreFile
                storePassword = targetKeystorePassword
                keyAlias = targetKeyAlias
                keyPassword = targetKeyPassword
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
            if (hasReleaseSigning) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    lint {
        disable += "AndroidGradlePluginVersion"
    }

    // Register generated web assets as an additional assets source directory
    sourceSets {
        getByName("main") {
            assets.srcDir(generatedAssetsRoot)
        }
    }
}

// Make preBuild depend on syncWebAssets so the generated assets are always
// present before the Android asset merger runs.
tasks.named("preBuild") {
    dependsOn(syncWebAssets)
}

dependencies {
    implementation("androidx.webkit:webkit:1.16.0")
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.json:json:20260719")
}
