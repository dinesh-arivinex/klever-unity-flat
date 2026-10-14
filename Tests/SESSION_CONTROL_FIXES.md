# Klever Unity — five requested fixes (14 October 2026)

## Changes

| Reported issue | Implementation |
| --- | --- |
| Live view does not change with selected experience | Simulation activation publishes the existing supported simulation mode and real URL. Reconnect republishes the current snapshot; pause state is included; observer sequence is scoped by publisher run/experience. Existing video/image/model/assessment modes are retained. |
| Welcome scene / Parent / MainPanel looks zoomed | KlassMeta Welcome canvas now scales a 1920×1080 reference using Expand. The intermediate Parent rect is corrected from 19200×10800 to 1920×1080; its MainPanel remains 1920×1080. Camera/world-space prefab assets are not globally rescaled. Visual acceptance needs Unity. |
| Resume restarts simulation | The iframe remains mounted; focus/input shield covers it. Hosted-page bridge freezes function timers, RAF/performance clock, running HTML media/animations and tracked Web Audio contexts; resume restores remaining timer time and the same document. The Unity countdown uses its existing scaled waits. Native worker/engine-specific or wall-clock code must consume the klever:simulation-pause event where needed. No claim of arbitrary third-party-page freeze. |
| Console Stop leaves simulation running | Close browser DOM before resetting pause/reloading; cancel the simulation timer without advancing assessment. Disable/destroy cleanup closes active simulations. Stop suppresses stale REST auto-start until a new console Start. |
| Console Start requires reopening app | Preserve the newest command snapshot across scene reload and restart from fresh scene dependencies. Deduplicate command IDs/ts to avoid QoS duplicate reload loops. Console and backend carry an additive action field, distinguishing Start from Resume even while paused. Existing role/owner checks and legacy controls remain. |

Console/backend scope extension was explicitly approved. Matching source changes: frontend device-connect Start/Pause/Resume handlers, frontend LiveStudentView simulation handshake/overlay, backend sessionControl service action validation/publication. No credentials, dependency packages, MQTT topics, database schema or authorization grants changed. These application sources are local changes; no Unity or console/backend application build has been deployed by this task.

## Verification

- `node --test Tests/WebGL/*.test.cjs Tests/Integration/*.test.cjs`: 15 tests pass. Integration tests need the sibling klever-backend checkout.
- `python Tests/verify-unity-source.py`: 9 real pure transition-policy cases pass; modified C# parses under native/editor/WebGL symbol combinations. This is **not** a Unity compile or scene playback test.
- Frontend TypeScript and targeted ESLint pass; backend Node syntax and integration checks pass.
- Kluster tools and Unity Editor are unavailable on this host. Kluster review, actual Unity build and scene/device UAT remain pending.

## Hosted simulations

Verified Klever AWS account through its existing klever-iam profile. The klass-vr-file bucket contains 26 root simulation entry pages: 13 CreateJS, 7 Unity and 6 other HTML entries. A read-only preparation captures original bytes/metadata/ETags and injects a content-hashed bridge before game scripts. The explicit rollout backs up originals, validates hashes/ETags, writes only the approved entries/assets and conditionally restores touched pages on failure. No bucket policy/ACL/versioning or EC2 infrastructure changes. Owner explicitly approved publishing this bridge; rollout evidence follows.

The hosted script validates parent source and origin, uses ready/state/ack messages, and is inert when opened standalone. Load it before engine scripts. Game clocks using workers, Date wall time or custom playback may need the documented pause event adapter; verify each deployed game on a real browser.

## Required Unity UAT after rebuilding

1. Select a video, image, model and simulation; verify the console changes to each matching mode.
2. Check Welcome > Parent > MainPanel at landscape and narrow aspect ratios.
3. During an interactive simulation, note its progress/countdown; Pause, wait, Resume; ensure progress/time continue without a new iframe load.
4. Stop while playing and while paused; ensure iframe/audio disappear and assessment does not start.
5. Start while playing and while paused; ensure a fresh scene/experience starts, without reopening the app. Resume must retain the current scene instead.
6. Repeat commands and reconnect MQTT; duplicate retained commands must not cause reload loops. Pause/Resume arriving during reload must be retained.

## Approved hosted rollout completed

- Published the bridge and updated all 26 entry pages in `s3://klass-vr-file/klass-simulations/`.
- Read-back SHA verification passed for all 26 entry pages, 26 bridge copies and 26 original backups.
- Bridge SHA256: `f1324d37064f3aa16748168206414916fbf478f54077bc7b9c9c499a0b9dcafa`.
- Original backup prefix: `s3://klass-vr-file/maintenance-backups/simulation-pause/20261014T102302Z-484ac3c3/`.
- Both targeted CloudFront invalidations completed: `E3RVEJSVVAPNCP / I8ALNT00RK1PMGZ7N3MNZF9Y82` and `E2ZG7TLSL56CS6 / IEY53EPNQFMKINIG5Y03SO0E74`.
- Real hosted Chrome smoke test could not obtain the ready handshake. A separate network check failed to resolve `d339ogzg199z0c.cloudfront.net` on this computer. Therefore actual hosted-browser playback is unverified; AWS read-back verification does not replace it.
- No Unity build, frontend release or backend release was deployed. Rebuild/release these application sources and complete the UAT steps above to activate and validate the complete console-to-device flow.
