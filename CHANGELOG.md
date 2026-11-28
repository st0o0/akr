# Changelog

## [0.1.1](https://github.com/st0o0/akr/compare/v0.1.0...v0.1.1) (2026-11-28)


### Features

* add Akka.Streams 1.6 parallel pipelines for source fetching and docs indexing ([a87e501](https://github.com/st0o0/akr/commit/a87e501327aef3d7034e5b6163edfb94e57dbf98))
* add docs search, version pinning, and source diff tools for community edition ([d6c8198](https://github.com/st0o0/akr/commit/d6c81983d39794f0be092b9ca1eadb83ad8cefb5))
* auto-detect Akka.NET version from project files + clean DI with IOptions ([e8598e4](https://github.com/st0o0/akr/commit/e8598e40ffab4e32ebdb19d3fe18e39bb9e05ba2))
* enable Native AOT publishing with unified JsonSerializerContext ([9c65aa3](https://github.com/st0o0/akr/commit/9c65aa3c93642aefa204348b0e17bb375243cc01))
* enable Native AOT release with RID-specific tool packaging ([b39f92e](https://github.com/st0o0/akr/commit/b39f92e997e9632718fcd14a9fc9aecda7c0508c))
* output truncation at 50k chars, fix multi-line declarations, improve config_diff empty message ([d12ed8f](https://github.com/st0o0/akr/commit/d12ed8f9f2ce410c62f6b4a371eeb83b1116a25b))


### Bug Fixes

* add RuntimeIdentifiers for pointer package restore on Linux CI ([98d4fe4](https://github.com/st0o0/akr/commit/98d4fe4e05bb81d3266539925387dacd32c9b78e))
* AOT condition uses rid check instead of loose bool, fix artifact download pattern ([02bc3ad](https://github.com/st0o0/akr/commit/02bc3adec20bcc4a71d932fe46ae1d43029281a9))
* RecordVersionAccess creates metadata when none exists, add edge case tests, improve tool response hints ([f739067](https://github.com/st0o0/akr/commit/f739067453e4a52cbcba39e5f511d93e9472169e))
* use platform-independent paths in GetRelativePosixPath test ([46af049](https://github.com/st0o0/akr/commit/46af0490fc4f429d8786c62697f082650c65432e))


### Refactoring

* code quality cleanup - LoggerMessage generators, NonBacktracking regex, remove section comments, eliminate ProjectContextInitializer ([c0f32eb](https://github.com/st0o0/akr/commit/c0f32eb8d58cbb4941aa31a07a7f21a9cc1da932))
* deduplicate tool helpers into HoconParser, HoconScanner, and TypeScanner ([7bd4200](https://github.com/st0o0/akr/commit/7bd420091212a483ad38b7df5a031a708d490b3d))
* inject IOptions into ProjectScanner, generate VersionInfo at build time ([f6d4564](https://github.com/st0o0/akr/commit/f6d4564865a90df423db085a1311127df193da8d))
* separation of concerns - split GitHubClient, reorganize tools by domain, switch to mono-repo docs ([337e43d](https://github.com/st0o0/akr/commit/337e43d6bc41d63fcf296777d2ec804a18480780))
* simplify release workflow - merge pointer into pack matrix, single publish step ([712a147](https://github.com/st0o0/akr/commit/712a147043fe4bcb7721be3e8de42b482ddd4f6e))

## [0.1.0](https://github.com/st0o0/akr/compare/v0.1.0...v0.1.0) (2026-11-23)


### ⚠ BREAKING CHANGES

* flatten abstractions into single GitHubClient, delete Akr.Core project

* remove all em dashes from markdown files ([a5cfade](https://github.com/st0o0/akr/commit/a5cfadefde1b3218e53ef37a18bfc6dbd886db92))


### Features

* add akr logo and NuGet package icon ([6a01a50](https://github.com/st0o0/akr/commit/6a01a506662474d6e683958fcf76e3da758b2509))
* add conditional source caching with TTL and ETag validation ([4e14658](https://github.com/st0o0/akr/commit/4e14658085fa399194fc3c315d6aa5e3e9569b0b))
* add config_reference and stream_stages MCP tools ([88129e1](https://github.com/st0o0/akr/commit/88129e149e30e8f4e650f3f368ccbb3ea9a6e3a9))
* add E2E test plan for MCP tool validation ([9f81f21](https://github.com/st0o0/akr/commit/9f81f217d7f1f3f6ecaa4777bc7a05c76524c12c))
* add Phase 2-5 tools (27 new MCP tools across deep intelligence, streams/persistence, config/migration, ecosystem) ([b8cc2ad](https://github.com/st0o0/akr/commit/b8cc2ad85df2233c20840dcb70270d0c2b5a950c))
* add type_members and hosting_extensions MCP tools ([4ee590a](https://github.com/st0o0/akr/commit/4ee590a32274db9b2b25a9bdfcd3dc665018123e))
* akr MCP server with search_source and lookup_type tools, AOT-ready, latest-by-default ([f20171e](https://github.com/st0o0/akr/commit/f20171eaf3feb166f3ed9657061b791a942a8dd0))


### Bug Fixes

* add contents:read permission to labels workflow ([d18f1fe](https://github.com/st0o0/akr/commit/d18f1fe2a0e9f8cc10fd872006ae863938cac233))
* atomic cache extraction, harden GitHubClient error handling, improve test coverage ([f65b3cd](https://github.com/st0o0/akr/commit/f65b3cd79250d9ddcd49c50ab15839be9a1cbe48))
* **ci:** use trivy-fs scan instead of image scan ([fa92e11](https://github.com/st0o0/akr/commit/fa92e11b7932c0f161806db271f72c2af037f88d))
* remove akr from local dotnet-tools.json, use global command in MCP config ([ef7d2fc](https://github.com/st0o0/akr/commit/ef7d2fc97d4fc348a168fed54d2d849bc46aacaa))
* resolve 6 E2E failures - config discovery, hosting mapping, inherited members, attribute defaults ([e37efff](https://github.com/st0o0/akr/commit/e37efff13c7b6737dae5713a277fb48c8e3ee536))
* update RepoRegistry for Akka.Hosting monorepo migration, add Reminders packages ([d0bab71](https://github.com/st0o0/akr/commit/d0bab713e38144aad7149792d0a18a00baf3a77b))
* update slopwatch baseline to schema v2 ([63633a5](https://github.com/st0o0/akr/commit/63633a599eff9b6b7cda5c3fa9933857005f6be0))


### Documentation

* rewrite README with all 33 tools, local dotnet tool workflow, update guidance ([83f4d45](https://github.com/st0o0/akr/commit/83f4d459c49177cda2ed18e82ab7c66bad1b08c7))


### Refactoring

* consolidate 11 tool files into 3 domain-grouped classes, add README ([e01f1b7](https://github.com/st0o0/akr/commit/e01f1b755698d4a84092c435f041e6f10b9874ca))
* deduplicate utilities, add caching, GeneratedRegex, fix package versions ([af2a6e0](https://github.com/st0o0/akr/commit/af2a6e0e04e8bf490240203aa5412cd706551ec5))
* extract 12 record types into Akr.Models namespace for separation of concerns ([dd24c90](https://github.com/st0o0/akr/commit/dd24c9066003eccc0286041f974929365114631e))
* extract HoconParser from FileService into Analysis layer ([d1941f3](https://github.com/st0o0/akr/commit/d1941f3929a794cf40a72708453f8bd64f527af4))
* extract TypeScanner from DeepIntelligenceTools into Analysis layer ([0e7006d](https://github.com/st0o0/akr/commit/0e7006d5831b9fd93dd85e84e232c18062b014e6))
* flatten abstractions into single GitHubClient, delete Akr.Core project ([427e4ce](https://github.com/st0o0/akr/commit/427e4ce46405b3753a927e0c1629f2c28a165736))
* introduce FileService with System.IO.Abstractions, eliminate all direct filesystem calls ([e16f6d8](https://github.com/st0o0/akr/commit/e16f6d88e3b12d2fea382159a0e407747eaf3f98))
* remove architecture test project, update AGENTS.md ([c46425e](https://github.com/st0o0/akr/commit/c46425ee7c48c709aee2b1050cdfe6558235904f))
* remove integration tests and native AOT, distribute as dotnet tool only ([d31be73](https://github.com/st0o0/akr/commit/d31be7351d7e45e7bf0cad0242f4ff42a7a5c3a1))
* review housekeeping - fix scan cache leak, deduplicate test helpers, dynamic UserAgent, fix rate-limit message ([931a274](https://github.com/st0o0/akr/commit/931a274915cf1e91dcb9f4cd416876c7b82f85df))
