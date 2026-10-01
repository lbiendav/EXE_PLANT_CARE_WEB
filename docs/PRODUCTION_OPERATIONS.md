# HomePlant production operations

This runbook is a release gate. Do not enable new live checkouts until every
item in the launch checklist has dated evidence and an owner.

## Required production configuration

- `ASPNETCORE_ENVIRONMENT=Production`
- `App__DeploymentStage=Production`
- `Firebase__ProjectId` and a least-privilege service account
- Firestore access for distributed sessions and Data Protection keys; no
  separate Redis service is required
- `App__PublicBaseUrl=https://homeplant-production.onrender.com`
- `Payments__Mode=Disabled` and `Payments__NewCheckoutsEnabled=false` until the
  controlled payment test succeeds
- Alert destination and on-call owner for application errors, `/readyz`, payment
  reconciliation, email delivery, and care-reminder failures

`/healthz` is process liveness. `/readyz` checks Firestore and the persistent
session prerequisite and must be monitored separately. Never expose credentials
or dependency exception text in either endpoint.

## Firestore backup and restore gate

Use the production project explicitly in every command. Never rely on the active
CLI project.

Create a daily managed backup schedule with 14-week retention:

```sh
gcloud firestore backups schedules create \
  --project=PRODUCTION_PROJECT_ID \
  --database='(default)' \
  --recurrence=daily \
  --retention=14w
```

Verify the schedule before launch and weekly thereafter:

```sh
gcloud firestore backups schedules list \
  --project=PRODUCTION_PROJECT_ID \
  --database='(default)'
```

At least quarterly, restore the latest backup to a separate recovery database or
isolated project. Record the backup identifier, start/end time, document counts,
application smoke-test result, RPO, RTO, operator, and deletion date of the
temporary recovery environment. A schedule without a successful restore record
does not satisfy this gate.

Images are stored inside `uploaded_images` and its `chunks` subcollections, so a
full Firestore backup includes the current image store.

## Live payment launch and recovery

1. Keep `Payments__NewCheckoutsEnabled=false`.
2. Deploy and verify `/healthz`, `/readyz`, persistent Firestore sessions,
   Firestore indexes, and logs.
3. Configure payOS secrets only in the hosting secret store.
4. Register the exact HTTPS webhook URL `/api/payments/payos/webhook`.
5. Add a single QA user to `Payments__PilotUserIds` and set `PilotOnly=true`.
6. Enable new checkouts and complete one minimum-value controlled transaction.
7. Confirm exactly one order, transaction, grant, subscription, receipt-delivery
   document, and email receipt.
8. Repeat while intentionally withholding the webhook. The reconciliation worker
   must recover the payment within its SLA without creating a duplicate grant.
9. Test cancel, expired order, provider timeout, duplicate callback, refund
   process, and alert delivery. Remove all QA-only entitlements using audited admin
   actions; do not delete financial evidence.

## Deployment and rollback

Before deploy: clean build, all automated checks, package vulnerability scan,
Firestore rules/index validation, publish inspection, and database backup.

Deploy by immutable commit. Confirm `/healthz` reports that exact commit, then run
public/authenticated smoke tests. If readiness, error rate, payment, or database
checks fail, disable new checkouts first and roll Render back to the last verified
commit. Schema changes must remain backward compatible with that commit.

## Evidence required to close the production gate

- Screenshot/export of the backup schedule and latest successful backup
- Latest isolated restore report
- Monitoring rules and successful test alert
- Controlled live payment and lost-webhook recovery report
- Guest, user, finance/support, content-admin, and super-admin E2E results
- Chrome, Safari, Firefox, Edge, iOS Safari, and Android Chrome smoke results
