## REMOVED Requirements

### Requirement: Shared file enumeration utility
**Reason**: Absorbed into `FileService.EnumerateFiles`. The standalone `FileEnumerator` static class is deleted.
**Migration**: Replace `FileEnumerator.EnumerateFiles(roots, pattern)` with `fileService.EnumerateFiles(roots, pattern)`.

### Requirement: Shared HOCON comment stripping
**Reason**: Absorbed into `FileService.StripHoconComment`. The standalone `HoconHelper` static class is deleted.
**Migration**: Replace `HoconHelper.StripComment(line)` with `fileService.StripHoconComment(line)`.

### Requirement: Convert static regex to GeneratedRegex
**Reason**: No change — `[GeneratedRegex]` patterns remain in their respective tool classes. This requirement is unaffected.
**Migration**: None needed.

### Requirement: Remove duplicate MSBuild properties
**Reason**: No change — already completed. This requirement is unaffected.
**Migration**: None needed.

### Requirement: Align Microsoft.Extensions.Hosting version
**Reason**: No change — already completed. This requirement is unaffected.
**Migration**: None needed.
