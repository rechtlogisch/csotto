![Recht logisch csotto banner image](rechtlogisch-csotto-banner.png)

# csotto

> Unofficial C# Otto download demo

An example of the Otto library implementation in C#.

> [!WARNING]  
> This demo is **not intended for productive usage**.

## Why?

In May 2024, ELSTER introduced a new library called Otto, designed for downloading objects from OTTER (Object Storage in ELSTER). ELSTER took this initiative because the existing solution had reached its limitations. Alongside Otto, a new version of Datenabholung v31 was also released.

The current method of data retrieval using ERiC is planned to be replaced on the client-side with ERiC version 41.2 by November 25th, 2024, at which point only Datenabholung v31 will be available. On the server-side, the transition will occur with the planned annual minimal version increase in mid-April 2025 (exact date to be announced in 2025). After this date, only ERiC version 41.2 or higher with Datenabholung v31 will be supported.

As a result, most software developers currently using ERiC Datenabholung will need to implement Otto in their production systems by April 2025.

This demo intends to simplify the transition and reduce implementation time.

## Usage

```bash
./csotto
  -u objectUuid         UUID of object to download (mandatory)
  -m size               Allocate provided Bytes of memory and download object in-memory (optional, max: 10485760 Bytes), cf. Download modes
  -e extension          Set filename extension of downloaded content [default: "txt"]
  -p password           Password for certificate [default: "123456"]
  -f                    Force file overwriting [default: false]
```

Examples:

```bash
csotto -u 468a69d4-0151-4681-9e8d-fcd87873d550 # ELOProtokoll / Lohnersatzleistung
csotto -u c48737b3-adfe-4e87-925c-7c362e00a416 -e pdf # DivaBescheidESt
```

> [!NOTE]  
> The code and scripts have been tested on Linux, macOS and Windows.

> [!TIP]  
> A list of object UUIDs is available with test certificates. You can get a list of them using `PostfachAnfrage` with the test certificate. The examples above are for `test-softorg-pse.pfx` and might be removed from the test instance after the time specified in the metadata.

## Vendor

You need the official ELSTER Otto library. Download the ERiC package >= v40 for your platform from the [ELSTER developer area](https://www.elster.de/elsterweb/entwickler/infoseite/eric), unzip it and place it at a desired path. Feel free to place it in `./vendor/`. You need two libraries: `otto` and `eSigner` (platform dependent naming: `(lib)otto.{so|dylib|dll}` and `(lib)eSigner.{so|dylib|dll}`).

> [!NOTE]  
> The ERiC package, especially the included there libraries are subject to a separate license agreement (presented before download in the ELSTER developer area and included in the ERiC package itself).

> [!TIP]  
> Choose the right library for the platform you compile and run on.

## Build with Docker

You can build the code with Docker using:

```bash
make docker-build
```

> [!TIP]  
> Place the Otto library in `./vendor/`. You could provide alternatively `LD_LIBRARY_PATH` environment variable on Linux or `DYLD_LIBRARY_PATH` on macOS during runtime.

## Build otherwise

Build with a tool of your choice and set up the project accordingly. Just don't forget to place the Otto library where the build solution can find it. For example `PATH`, typical places where libraries are searched on your system or just place it next to the build executable. 

Here is an example howto run `csotto` locally on macOS after installing [.NET SDK 8.0](https://learn.microsoft.com/en-us/dotnet/core/install/macos).

```bash
# Clone repository
git clone git@github.com:rechtlogisch/csotto.git

# Change to directory with source code
cd csotto

# Retrieve test certificate to "certificate" subdirectory
./get-test-certificate.sh

# Build with dotnet
dotnet build

# Run `csotto`, provide objectUuid with -u option, DYLD_LIBRARY_PATH pointing to Otto library and your DEVELOPER_ID inline
DYLD_LIBRARY_PATH="./vendor" DEVELOPER_ID="00000" dotnet run -u 468a69d4-0151-4681-9e8d-fcd87873d550
```

> [!NOTE]  
> You should set your five-digit Developer-ID (German: Hersteller-ID) as the environment variable `DEVELOPER_ID`. You could source it from for example `.env` or pass it inline to `csotto`, as shown in the steps above.

> [!TIP]  
> The downloaded result will be saved in the same directory as `csotto`, unless you provide a different `PATH_DOWNLOAD`.

## Environment variables

All supported environment variables are listed in [`.env.example`](.env.example). Feel free to copy them to `.env`, adjust accordingly and source for usage.

## Download modes

The demo showcases two methods for downloading objects: blockwise (default) and in-memory. OTTER and Otto operate by design by streaming data and forwarding it to the desired storage blockwise. That is the optimal and memory-efficient way for large files. ELSTER engineers wrapped all the necessary calls and the download workflow in one function: `OttoDatenAbholen()`, which simplifies the implementation and temporarily stores the complete object in memory.

This demo can operate in both modes. To download in-memory, add the option `-m` with a value exceeding `0` and not exceeding `10485760` Bytes (10 MiB). It is recommended to use the in-memory mode with objects where the final size is known and does not exceed the arbitrarily set size of 10485760 Bytes.

> [!IMPORTANT]  
> `-m` sets the minimal allocated memory size. When the object is larger than the set size, Otto allocates as much as needed and as much as available memory. Use at your own risk.

## Docker

A simple [Dockerfile](Dockerfile) is included. You can use `make docker-build` and `make docker-csotto` to build and run `csotto` in a container.

> [!TIP]  
> Mount volumes, set `PATH_DOWNLOAD` and `PATH_LOG` environment variables to expose data outside the container.

## Changelog

Please see [CHANGELOG](CHANGELOG.md) for more information on what has changed recently.

## Contributing

Please see [CONTRIBUTING](https://github.com/rechtlogisch/.github/blob/main/CONTRIBUTING.md) for details.

## Security Vulnerabilities

If you discover any security-related issues, please email open-source@rechtlogisch.de instead of using the issue tracker.

## Credits

- [Krzysztof Tomasz Zembrowski](https://github.com/rechtlogisch)
- [All Contributors](../../contributors)

## License

The MIT License (MIT). Please see [License File](LICENSE.md) for more information.

The ERiC package, especially libraries, is not included in this repository and is subject to a separate license agreement. Please see the [ELSTER developer area](https://www.elster.de/elsterweb/entwickler/infoseite/eric) or the lizenz.pdf included in the ERiC package for more information.

## Disclaimer

This demo was developed by [RL Recht logisch GmbH & Co. KG](https://rechtlogisch.de/impressum/) and should be used only for test purposes.

ELSTER is a registered trademark of the Freistaat Bayern, represented by the Bayerische Staatsministerium der Finanzen.
