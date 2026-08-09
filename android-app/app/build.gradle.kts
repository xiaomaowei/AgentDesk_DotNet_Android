import org.gradle.api.tasks.Exec
import org.gradle.api.tasks.Sync

plugins {
    id("com.android.application")
}

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
        versionCode = 1
        versionName = "0.1.0"

        testInstrumentationRunner = "android.test.InstrumentationTestRunner"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
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
