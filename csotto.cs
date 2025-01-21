using System;
using System.IO;
using System.Runtime.InteropServices;

namespace csotto;

internal static class Common
{
    public static int Error(string message, OttoStatusCode code)
    {
        Console.WriteLine("[ERROR] " + message);
        Console.WriteLine("[CODE]  " + (int)code + " (" + code + ")");
        return (int)code;
    }

    public static int DownloadError(OttoStatusCode code)
    {
        string message = code switch
        {
            OttoStatusCode.OTTO_TRANSFER_UNAUTHORIZED => "The client is not allowed to use the API.",
            OttoStatusCode.OTTO_TRANSFER_NOT_FOUND => "The OTTER server did not find the object.",
            _ => "Error occurred while downloading. Check otto.log for details."
        };
        return Error(message, code);
    }
}

internal class CsottoBlockwise : IDisposable
{
    private readonly IntPtr instance;
    private readonly IntPtr certificateHandle;
    private IntPtr contentHandle;
    private IntPtr downloadHandle;

    private bool disposed;

    public CsottoBlockwise(string pathLog, string pathCertificate, string certificatePassword, string proxyUrl)
    {
        // Create instance
        OttoStatusCode statusCodeInstanceCreate = Native.OttoInstanzErzeugen(pathLog, IntPtr.Zero, IntPtr.Zero, out instance);
        if (statusCodeInstanceCreate != OttoStatusCode.OTTO_OK)
        {
            Common.Error("Could not create an Otto instance. Check otto.log for details.", statusCodeInstanceCreate);
        }

        // Set proxy
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            OttoStatusCode statusCodeProxy = Native.OttoProxyKonfigurationSetzen(instance, new OttoProxyKonfiguration { version = 1, url = proxyUrl });
            if (statusCodeProxy != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not set proxy configuration. Check otto.log for details.", statusCodeProxy);
            }
        }

        // Open certificate
        OttoStatusCode statusCodeCertificateOpen = Native.OttoZertifikatOeffnen(instance, pathCertificate, certificatePassword, out certificateHandle);
        if (statusCodeCertificateOpen != OttoStatusCode.OTTO_OK)
        {
            Common.Error("Could not open certificate: " + pathCertificate, statusCodeCertificateOpen);
        }
        Console.WriteLine("[INFO]  Using certificate path: " + pathCertificate);
    }

    public int Workflow(string objectUuid, string developerId, string fileExtension, string pathDownload)
    {
        // Start download
        OttoStatusCode statusCodeDownloadStart = Native.OttoEmpfangBeginnen(instance, objectUuid, certificateHandle, developerId, out downloadHandle);
        if (statusCodeDownloadStart != OttoStatusCode.OTTO_OK)
        {
            return Common.Error("Could not start download. Check otto.log", statusCodeDownloadStart);
        }

        // Create content buffer
        OttoStatusCode statusCodeContentHandleCreate = Native.OttoRueckgabepufferErzeugen(instance, out contentHandle);
        if (statusCodeContentHandleCreate != OttoStatusCode.OTTO_OK)
        {
            return Common.Error("Could not create handle for content.", statusCodeContentHandleCreate);
        }

        string filepath = Path.Combine(pathDownload, objectUuid + "." + fileExtension);
        FileStream file;

        try
        {
            file = File.Open(filepath, FileMode.Append, FileAccess.Write, FileShare.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open file: " + filepath);
            Console.WriteLine("Exception: " + ex.Message);
            return (int)CsottoReturnCode.OUTPUT_FILE_OPEN_FAILED;
        }

        // Continue download
        OttoStatusCode statusCodeDownloadContinue;
        do
        {
            statusCodeDownloadContinue = Native.OttoEmpfangFortsetzen(downloadHandle, contentHandle);
            if (statusCodeDownloadContinue != OttoStatusCode.OTTO_OK)
            {
                break;
            }

            ulong contentSize = Native.OttoRueckgabepufferGroesse(contentHandle);
            if (contentSize <= 0)
            {
                break;
            }

            Console.WriteLine("[INFO]  Downloaded: " + contentSize + " Bytes");
            byte[] contentBlock = new byte[contentSize];
            Marshal.Copy(Native.OttoRueckgabepufferInhalt(contentHandle), contentBlock, 0, (int)contentSize);
            file.Write(contentBlock, 0, (int)contentSize);
        } while (true);

        file.Close();

        if (statusCodeDownloadContinue != OttoStatusCode.OTTO_OK)
        {
            File.Delete(filepath);
            return Common.DownloadError(statusCodeDownloadContinue);
        }

        Console.WriteLine("[INFO]  Downloaded content saved in: " + filepath);
        return (int)CsottoReturnCode.OK;
    }

    ~CsottoBlockwise()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        // End download
        if (downloadHandle != IntPtr.Zero)
        {
            OttoStatusCode statusCodeDownloadEnd = Native.OttoEmpfangBeenden(downloadHandle);
            if (statusCodeDownloadEnd != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not end download.", statusCodeDownloadEnd);
            }
        }

        // Close certificate
        if (certificateHandle != IntPtr.Zero)
        {
            OttoStatusCode statusCodeCertificateClose = Native.OttoZertifikatSchliessen(certificateHandle);
            if (statusCodeCertificateClose != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not close certificate handle", statusCodeCertificateClose);
            }
        }

        // Release content buffer
        if (contentHandle != IntPtr.Zero)
        {
            OttoStatusCode statusCodeContentRelease = Native.OttoRueckgabepufferFreigeben(contentHandle);
            if (statusCodeContentRelease != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not release content handle.", statusCodeContentRelease);
            }
        }

        // Destroy instance
        if (instance != IntPtr.Zero)
        {
            OttoStatusCode statusCodeInstanceDestroy = Native.OttoInstanzFreigeben(instance);
            if (statusCodeInstanceDestroy != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not destroy the Otto instance. Check otto.log for details.", statusCodeInstanceDestroy);
            }
        }

        disposed = true;
    }
}

internal class CsottoInMemory : IDisposable
{
    private readonly IntPtr instance;
    private readonly IntPtr contentHandle;
    private readonly string pathCertificate;
    private readonly string certificatePassword;

    private bool disposed;

    public CsottoInMemory(string pathLog, string providedPathCertificate, string providedCertificatePassword, string proxyUrl)
    {
        // Create instance
        OttoStatusCode statusCodeInstanceCreate =
            Native.OttoInstanzErzeugen(pathLog, IntPtr.Zero, IntPtr.Zero, out instance);
        if (statusCodeInstanceCreate != OttoStatusCode.OTTO_OK)
        {
            Common.Error("Could not create an Otto instance. Check otto.log for details.", statusCodeInstanceCreate);
        }

        // Set proxy
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            OttoStatusCode statusCodeProxy = Native.OttoProxyKonfigurationSetzen(instance, new OttoProxyKonfiguration { version = 1, url = proxyUrl });
            if (statusCodeProxy != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not set proxy configuration. Check otto.log for details.", statusCodeProxy);
            }
        }

        // Create content buffer
        OttoStatusCode statusCodeContentHandleCreate = Native.OttoRueckgabepufferErzeugen(instance, out contentHandle);
        if (statusCodeContentHandleCreate != OttoStatusCode.OTTO_OK)
        {
            Common.Error("Could not create handle for content.", statusCodeContentHandleCreate);
        }

        pathCertificate = providedPathCertificate;
        certificatePassword = providedCertificatePassword;
    }

    public int Workflow(string objectUuid, uint size, string developerId, string fileExtension, string pathDownload)
    {
        string filepath = Path.Combine(pathDownload, objectUuid + "." + fileExtension);
        FileStream file;

        try
        {
            file = File.Open(filepath, FileMode.Append, FileAccess.Write, FileShare.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open file: " + filepath);
            Console.WriteLine("Exception: " + ex.Message);
            return (int)CsottoReturnCode.OUTPUT_FILE_OPEN_FAILED;
        }

        Console.WriteLine("[INFO]  Using certificate path: " + pathCertificate);

        OttoStatusCode statusCodeDownload = Native.OttoDatenAbholen(
            instance,
            objectUuid,
            size,
            pathCertificate,
            certificatePassword,
            developerId,
            null,
            contentHandle
        );

        if (statusCodeDownload != OttoStatusCode.OTTO_OK)
        {
            File.Delete(filepath);
            file.Close();
            return Common.DownloadError(statusCodeDownload);
        }

        ulong contentSize = Native.OttoRueckgabepufferGroesse(contentHandle);
        if (contentSize > 0)
        {
            Console.WriteLine("[INFO]  Downloaded: " + contentSize + " bytes");
            byte[] contentBlock = new byte[contentSize];
            Marshal.Copy(Native.OttoRueckgabepufferInhalt(contentHandle), contentBlock, 0, (int)contentSize);
            file.Write(contentBlock, 0, (int)contentSize);
            Console.WriteLine("[INFO]  Downloaded content saved in: " + filepath);
        }

        file.Close();

        return (int)CsottoReturnCode.OK;
    }

    ~CsottoInMemory()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        // Release content buffer
        if (contentHandle != IntPtr.Zero)
        {
            OttoStatusCode statusCodeContentRelease = Native.OttoRueckgabepufferFreigeben(contentHandle);
            if (statusCodeContentRelease != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not release content handle.", statusCodeContentRelease);
            }
        }

        // Destroy instance
        if (instance != IntPtr.Zero)
        {
            OttoStatusCode statusCodeInstanceDestroy = Native.OttoInstanzFreigeben(instance);
            if (statusCodeInstanceDestroy != OttoStatusCode.OTTO_OK)
            {
                Common.Error("Could not destroy the Otto instance. Check otto.log for details.", statusCodeInstanceDestroy);
            }
        }

        disposed = true;
    }
}

internal static class Native
{
    [DllImport("otto", CharSet = CharSet.Ansi, BestFitMapping = true,
        ThrowOnUnmappableChar = false, CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoInstanzErzeugen(
        [MarshalAs(UnmanagedType.LPStr)] string logPfad,
        IntPtr logCallback,
        IntPtr logCallbackBenutzerdaten,
        out IntPtr instanz);

    [DllImport("otto", CharSet = CharSet.Ansi, BestFitMapping = true,
        ThrowOnUnmappableChar = false, CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoZertifikatOeffnen(
        IntPtr instanz,
        [MarshalAs(UnmanagedType.LPStr)] string zertifikatsPfad,
        [MarshalAs(UnmanagedType.LPStr)] string zertifikatsPasswort,
        out IntPtr zertifikat);


    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoProxyKonfigurationSetzen(
        IntPtr instanz,
        OttoProxyKonfiguration proxyKonfiguration);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoEmpfangBeginnen(
        IntPtr instanz,
        [MarshalAs(UnmanagedType.LPStr)] string objektId,
        IntPtr zertifikat,
        [MarshalAs(UnmanagedType.LPStr)] string herstellerId,
        out IntPtr empfang);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoRueckgabepufferErzeugen(
        IntPtr instanz,
        out IntPtr rueckgabepuffer);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern ulong OttoRueckgabepufferGroesse(IntPtr rueckgabepuffer);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr OttoRueckgabepufferInhalt(IntPtr rueckgabepuffer);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoEmpfangFortsetzen(
        IntPtr empfang,
        IntPtr datenBlock);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoEmpfangBeenden(IntPtr empfang);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoZertifikatSchliessen(IntPtr zertifikat);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoRueckgabepufferFreigeben(IntPtr rueckgabepuffer);

    [DllImport("otto", CharSet = CharSet.Ansi, BestFitMapping = true,
        ThrowOnUnmappableChar = false, CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoInstanzFreigeben(IntPtr instanz);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    public static extern OttoStatusCode OttoDatenAbholen(
        IntPtr instanz,
        [MarshalAs(UnmanagedType.LPStr)] string objektId,
        [MarshalAs(UnmanagedType.U4)] uint objektGroesse,
        [MarshalAs(UnmanagedType.LPStr)] string zertifikatsPfad,
        [MarshalAs(UnmanagedType.LPStr)] string zertifikatsPasswort,
        [MarshalAs(UnmanagedType.LPStr)] string herstellerId,
        [MarshalAs(UnmanagedType.LPStr)] string abholzertifikat,
        IntPtr abholDaten);
}

[StructLayout(LayoutKind.Sequential)]
public class OttoProxyKonfiguration
{
    [MarshalAs(UnmanagedType.I4)]
    public Int32 version;
    [MarshalAs(UnmanagedType.LPStr)]
    public string url;
    [MarshalAs(UnmanagedType.LPStr)]
    public string benutzerName;
    [MarshalAs(UnmanagedType.LPStr)]
    public string benutzerPassword;
    [MarshalAs(UnmanagedType.LPStr)]
    public string authenifizierungsMethode;
}

public enum OttoStatusCode
{
    OTTO_OK = 0,
    OTTO_UNBEKANNTER_FEHLER = 610401002,
    OTTO_TRANSFER_UNAUTHORIZED = 610403007,
    OTTO_TRANSFER_NOT_FOUND = 610403008,
}

public enum CsottoReturnCode
{
    OK = 0,
    TOO_FEW_ARGUMENTS = 1,
    UNSUPPORTED_ARGUMENT = 2,
    OBJECT_UUID_MISSING = 3,
    DEVELOPER_ID_MISSING = 4,
    FILE_EXISTS = 5,
    OUTPUT_FILE_OPEN_FAILED = 6,
}

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine(" -u objectUuid\t\tUUID of object to download (mandatory)");
            Console.Error.WriteLine(" -m size\t\tAllocate provided Bytes of memory and download object in-memory (optional, max: 10485760 Bytes), when not provided or exceeds max download blockwise");
            Console.Error.WriteLine(" -e extension\t\tSet filename extension of downloaded content [default: \"txt\"]");
            Console.Error.WriteLine(" -p password\t\tPassword for certificate [default: \"123456\"]");
            Console.Error.WriteLine(" -y proxy\t\tProxy URL for communucation with the OTTER server (optional, by default no proxy is being set within Otto)");
            Console.Error.WriteLine(" -f\t\t\tForce file overwriting [default: false]");
            Console.Error.WriteLine(" -o\t\t\tSpecify Url of the proxy server Otto should use");
            return (int)CsottoReturnCode.TOO_FEW_ARGUMENTS;
        }

        string objectUuid = null;
        uint memorySizeAllocation = 0;
        string fileExtension = "txt";
        string certificatePassword = "123456";
        bool forceOverwrite = false;
        string proxyUrl = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-u":
                    objectUuid = args[++i];
                    break;
                case "-m":
                    if (!int.TryParse(args[++i], out int parsedMemorySizeAllocation) || parsedMemorySizeAllocation <= 0)
                    {
                        Console.Error.WriteLine("Invalid memory size allocation. It must be an integer greater than 0.");
                        return (int)CsottoReturnCode.UNSUPPORTED_ARGUMENT;
                    }
                    memorySizeAllocation = (uint)parsedMemorySizeAllocation;
                    break;
                case "-e":
                    fileExtension = args[++i];
                    break;
                case "-p":
                    certificatePassword = args[++i];
                    break;
                case "-y":
                    proxyUrl = args[++i];
                    break;
                case "-f":
                    forceOverwrite = true;
                    break;
                case "-o":
                    proxyUrl = args[++i];
                    break;
                default:
                    Console.Error.WriteLine("Unsupported option: " + args[i]);
                    return (int)CsottoReturnCode.UNSUPPORTED_ARGUMENT;
            }
        }

        if (objectUuid == null)
        {
            Console.Error.WriteLine("Object UUID is missing. Please provide it with the -u flag.");
            return (int)CsottoReturnCode.OBJECT_UUID_MISSING;
        }

        string pathDownload = Environment.GetEnvironmentVariable("PATH_DOWNLOAD") ?? ".";
        string filepath = Path.Combine(pathDownload, objectUuid + "." + fileExtension);
        if (File.Exists(filepath))
        {
            if (forceOverwrite)
            {
                File.Delete(filepath);
            }
            else
            {
                Console.WriteLine("File: " + filepath + " already exists. Do you want to overwrite it? (y/n)");
                char userChoice = (char)Console.Read();
                if (userChoice is 'y' or 'Y')
                {
                    File.Delete(filepath);
                }
                else
                {
                    Console.Error.WriteLine("File: " + filepath + " was not overwritten. Stopping.");
                    return (int)CsottoReturnCode.FILE_EXISTS;
                }
            }
        }

        string developerId = Environment.GetEnvironmentVariable("DEVELOPER_ID");
        if (string.IsNullOrEmpty(developerId))
        {
            Console.Error.WriteLine("DEVELOPER_ID environment variable missing. Please set it accordingly.");
            return (int)CsottoReturnCode.DEVELOPER_ID_MISSING;
        }

        string pathCertificate = Environment.GetEnvironmentVariable("PATH_CERTIFICATE") ?? "certificate/test-softorg-pse.pfx";
        string pathLog = Environment.GetEnvironmentVariable("PATH_LOG") ?? ".";
        if (string.IsNullOrEmpty(proxyUrl))
        {
            proxyUrl = Environment.GetEnvironmentVariable("PROXY_URL") ?? null;
        }

        if (memorySizeAllocation is > 0 and <= 10485760)
        {
            Console.WriteLine("[INFO]  Using simplified in-memory data retrieval for objects smaller than 10485760 Bytes (10 MiB)");
            CsottoInMemory csotto = new CsottoInMemory(pathLog, pathCertificate, certificatePassword, proxyUrl);
            int result = csotto.Workflow(objectUuid, memorySizeAllocation, developerId, fileExtension, pathDownload);
            csotto.Dispose();
            return result;
        }
        else
        {
            Console.WriteLine("[INFO]  Using blockwise data retrieval");
            CsottoBlockwise csotto = new CsottoBlockwise(pathLog, pathCertificate, certificatePassword, proxyUrl);
            int result = csotto.Workflow(objectUuid, developerId, fileExtension, pathDownload);
            csotto.Dispose();
            return result;
        }
    }
}
