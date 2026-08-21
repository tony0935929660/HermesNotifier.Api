# Changelog

## [0.1.1] - 2026-08-21

### Fixed

- Synchronize complete product metadata, availability, and cache expiry before sending restock notifications.
- Keep products without a valid price pending so incomplete data cannot change availability or trigger notifications.
- Trigger restock notifications when either availability field changes to in stock.

## [0.1.0] - 2026-08-03

### Changed

- Store and return product identifiers in the full H-prefixed Hermès SKU format.
- Normalize incoming product identifiers by adding a missing H prefix for backward compatibility.
- Validate variant product URLs and admin URL imports against H-prefixed identifiers.

## 2026-08-01

### Added

- Persist final scraper failures with product, verdict, provider tier, retry, and timing details.
- Add an API endpoint for recording scraper failures without changing product availability.
- Add batch product variant synchronization with validated Hermès product URLs.
- Create newly discovered variants as pending products for a full initial scrape.

### Changed

- Synchronize availability for existing variants and send restock notifications in one batch.
- Keep pending variants immediately eligible for metadata and price enrichment.