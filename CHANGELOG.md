# Changelog

## 2026-08-01

### Added

- Persist final scraper failures with product, verdict, provider tier, retry, and timing details.
- Add an API endpoint for recording scraper failures without changing product availability.
- Add batch product variant synchronization with validated Hermès product URLs.
- Create newly discovered variants as pending products for a full initial scrape.

### Changed

- Synchronize availability for existing variants and send restock notifications in one batch.
- Keep pending variants immediately eligible for metadata and price enrichment.