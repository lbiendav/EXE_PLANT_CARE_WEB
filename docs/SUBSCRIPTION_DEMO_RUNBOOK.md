# HomePlant subscription demo runbook

## Safe defaults

The feature ships closed: `Subscriptions__Enabled=false`, `Subscriptions__EnforceLimits=false`, `Payments__Mode=Disabled`, and `Payments__DemoEnabled=false`. A demo subscription never grants paid entitlements when `App__DeploymentStage=Production`.

## Local or staging demo configuration

Set `App__DeploymentStage` to `Local` or `Staging`, enable subscriptions, set payment mode to `Demo`, enable the demo flag, and add the exact Firebase test project ID to `Payments__AllowedDemoProjectIds__0`. The Firestore emulator is also accepted as a test data environment. Do not use a production Firebase project.

Bank QR remains optional. Configure `Payments__Bank__Bin`, `Payments__Bank__AccountNumber`, and `Payments__Bank__AccountName` together to render a VietQR image. The simulator does not inspect or confirm a bank transfer; the demo must stop before sending money.

## Demo flow

1. Open `/Plans`, choose a tier and duration, and sign in if required.
2. Review the server-created order at `/Checkout/{orderId}`.
3. Select **Mô phỏng thanh toán thành công** once. Repeating the POST returns the stored activation and does not add time again.
4. Open `/Subscription` to verify the tier, expiry, usage, and order history.

## Enforcement rollout

Before setting `Subscriptions__EnforceLimits=true`, create `users/{uid}/usage_state/current` with the exact current `plantCount` for every existing user and reconcile it against each `user_plants` subcollection. Pause plant create/delete during that maintenance window. Missing counters fail closed after enforcement is enabled; existing plants are never deleted.

Deploy `firestore.indexes.json` to the selected staging project for order history. Keep writes to subscription, order, usage, AI request, and payment event collections server-only in the applicable Firestore ruleset.

## Rollback

Set `Subscriptions__Enabled=false` to stop new orders, `Payments__DemoEnabled=false` (or mode `Disabled`) to stop confirmation, and `Subscriptions__EnforceLimits=false` to stop quota rejection. Keep stored orders and subscriptions for reconciliation; do not delete or reseed them.

## Verification

Run `dotnet run --project Tests/SubscriptionChecks` plus the existing regression commands documented in the main plan. Emulator concurrency and HTTP authorization tests require a dedicated test Firebase project/emulator and are not run against live data.
