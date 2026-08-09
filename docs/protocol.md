# AgentDeck Protocol v1

AgentDeck clients exchange UTF-8 JSON envelopes with the Bridge. Android HTTP actions use the same action envelope; [the JSON Schema](../protocol/agentdeck-v1.schema.json) is authoritative.

Every envelope includes `version`, `type`, `id`, `timestamp`, and a type-specific `payload`.

| Direction | Type | Purpose |
| --- | --- | --- |
| Bridge → client | `state` | Current selected agent/session state |
| Client → Bridge | `action` | Navigation, usage, clear, approve, or reject |
| Bridge → client | `action_result` | Whether an action was accepted |
| Either | `ping`, `pong` | Reserved health messages |
| Bridge → client | `error` | Invalid message details |

Actions include `next`, `next_project`, `previous_project`, `select_project`, `usage`, `usage_next`, `clear`, `approve`, and `reject`. Approval actions must use the current `target_id`; stale or unknown identifiers are rejected.

Idle state can include derived local usage fields such as `usage_remaining_percent`, `usage_reset_date`, `usage_provider`, five-hour values, and `usage_buckets`. These never contain account identifiers, tokens, or raw CLI output. Clients must ignore unknown future message types. Complete examples are under `protocol/examples/`.
