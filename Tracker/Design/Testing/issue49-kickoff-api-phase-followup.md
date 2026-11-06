# Issue 49 follow-up: Game Controller API phases

This note tracks a live readiness mismatch found while exercising the existing match-mode AppHost. It does not amend the Issue 49 acceptance or the approved design.

## Observed run

- Source: design base `59320715a1e5bca6ef9ac33aba74a3ecddbb987c`; Game Controller image `robocupssl/ssl-game-controller:3.20.3`.
- Run mode: `Testing:Mode=match`, `Testing:MatchReadinessOnly=false`; the match-controller container was present and enabled.
- Result: failed at `readiness` with `apiReferee=NoObservation, visionReady=True, participantResourcesReady=True`; no match start/termination or result pass occurred.
- Read-only Game Controller WebSocket observation included `stage=NORMAL_FIRST_HALF_PRE`, `command=KICKOFF`, `gameState=KICKOFF`. The first observed messages were 849, 3433, and 1603 bytes, so they did not exceed the client’s current 4096-byte limit.
- The corresponding controller health response showed fresh decoded referee UDP input but no normalized API observation.

## Narrow follow-up

`GameControllerStateMapping` currently accepts `gameState` values HALT, STOP, and RUNNING, and matches KICKOFF only with STOP. The observed `KICKOFF/KICKOFF` state therefore normalizes to no observation. The pinned Game Controller v3.20.3 state machine also keeps the prior game state when applying NORMAL_START, so the valid `NORMAL_START/KICKOFF` interval must remain observable until play reaches RUNNING. See [v3.20.3 change_command.go](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/internal/app/statemachine/change_command.go).

The implementation follow-up should add only the observed kickoff/start phase pairs and focused tests based on the captured API shape, preserve STOP/HALT/ForceStart/unknown-state behavior, and then repeat the actual match run. This note records a tracking baseline only; no code or tests are changed by it.

## `POST_GAME`

```json
{
  "status": "passed",
  "stageReached": "post_game",
  "terminationKind": "PostGame",
  "finalReadiness": { "isComplete": true }
}
```

[PR #116](https://github.com/ibis-ssl/Duck/pull/116)
