# Changelog

All notable changes to `csotto` will be documented in this file.

## v1.2.3 - 2025-04-08
- Add OttoHoleFehlertext (works only with Otto >= 41.4), remove DownloadError() and change Error()
- Changed calling convention to Cdecl to support Otto on 32-Bit Windows systems
- Tested and runnable with Otto 41.5 and eSigner 62.0.0.5
- Adjustments in README

## v1.2.2 - 2025-01-21
- Add flag -y for setting a proxy url to use while communicating with the OTTER servers
- Add PROXY_URL environment variable (-y overrides it)
- Tested and runnable with eSigner 60.0.1.2 (published in ERiC 41.3, containing Otto 41.2)
- Adjustments in README

## v1.2.1 - 2024-11-27
- Tested and runnable with Otto 41.2.6
- Adjustments in README

**Full Changelog**: https://github.com/rechtlogisch/csotto/compare/v1.2.0...v1.2.1

## v1.2.0 - 2024-10-25
- Utilizes Otto 41.1.3
- Adds flag -m, which incorporates a newly introduced simplified function to download objects in-memory, instead of downloading them blockwise
- Adjustments in READMEs and help text

**Full Changelog**: https://github.com/rechtlogisch/csotto/compare/v1.1.0...v1.2.0

## v1.1.0 - 2024-07-29
- Utilizes Otto 40.2.8
- Supports most security tokens (Abholzertifikat not implemented in this demo)
- Default certificate now: test-softorg-pse.pfx

**Full Changelog**: https://github.com/rechtlogisch/csotto/compare/v1.0.0...v1.1.0

## v1.0.0 - 2024-06-08
- Initial version
- Utilizes Otto 40.1.8

**Full Changelog**: https://github.com/rechtlogisch/csotto/commits/v1.0.0
