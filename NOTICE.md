# Attribution and license notices

## LibrusApixNet

Copyright (C) 2026 LibrusApixNet contributors, for the C# port and its additions.

LibrusApixNet is a C#/.NET 10 port of [librus-apix](https://github.com/RustySnek/librus-apix), originally authored by Pascal Jodłowski and maintained with the librus-apix contributors. The module layout, behavior and two descriptive-grade regression scenarios were adapted from that project. Port-specific changes are documented in `docs/PORTING.md` and `docs/PORTING.en.md` in the source distribution.

## librus-apix (original library)

- Original author: **Pascal Jodłowski**, as recorded in the upstream [author metadata](https://github.com/RustySnek/librus-apix/blob/c54f05f3b483627328852825fff3e46fab27e87d/setup.cfg).
- Upstream repository: [RustySnek/librus-apix](https://github.com/RustySnek/librus-apix), developed with the librus-apix contributors.
- C# port and additions: **LibrusApixNet contributors**. This is a modified port, independently maintained from the original Python library.

- Upstream release: **1.6.0**.
- Upstream baseline: `c54f05f3b483627328852825fff3e46fab27e87d`.
- Baseline date: 2026-10-10.
- Baseline subject: `fix: replace [attr!=] selectors with :not() for soupsieve 3.0 compat`.
- C# port and publication preparation date: 2026-10-11.
- Port license: **GNU General Public License, version 3 only** (`GPL-3.0-only`).

The full license is included in `LICENSE`. This port follows the [GPLv3 LICENSE at the pinned upstream revision](https://github.com/RustySnek/librus-apix/blob/c54f05f3b483627328852825fff3e46fab27e87d/LICENSE). The upstream [setup.cfg at the same revision](https://github.com/RustySnek/librus-apix/blob/c54f05f3b483627328852825fff3e46fab27e87d/setup.cfg) contains an inconsistent MIT license label and classifier. That discrepancy is recorded here; MIT is not the license of this port.

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3 of the License.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

LibrusApixNet is an independent, unofficial project. It is not affiliated with or endorsed by Librus or the upstream project. Product names belong to their respective owners.

## AngleSharp

The library depends on [AngleSharp 1.8.4](https://www.nuget.org/packages/AngleSharp/1.8.4), copyright 2013–2026 AngleSharp, licensed under the [MIT License](https://github.com/AngleSharp/AngleSharp/blob/32981ded5b0c78c4a5afcfbb677ffb329078fdf8/LICENSE).

AngleSharp remains a separate NuGet dependency; its assembly is not embedded in the LibrusApixNet package. Its own license remains MIT.
