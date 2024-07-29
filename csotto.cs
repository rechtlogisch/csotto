using System;
using System.IO;
using System.Runtime.InteropServices;

namespace csotto;

internal class Csotto : IDisposable
{
    private readonly IntPtr instance;
    private readonly IntPtr certificateHandle;
    private IntPtr contentHandle;
    private IntPtr downloadHandle;

    private bool disposed;

    public Csotto(string pathLog, string pathCertificate, string certificatePassword)
    {
        // Create instance
        OttoStatusCode statusCodeInstanceCreate = OttoInstanzErzeugen(pathLog, IntPtr.Zero, IntPtr.Zero, out instance);
        if (statusCodeInstanceCreate != OttoStatusCode.OTTO_OK)
        {
            Error("Could not create an Otto instance. Check otto.log for details.", statusCodeInstanceCreate);
        }

        // Open certificate
        OttoStatusCode statusCodeCertificateOpen = OttoZertifikatOeffnen(instance, pathCertificate, certificatePassword, out certificateHandle);
        if (statusCodeCertificateOpen != OttoStatusCode.OTTO_OK)
        {
            Error("Could not open certificate: " + pathCertificate, statusCodeCertificateOpen);
        }
        Console.WriteLine("[INFO]  Using certificate path: " + pathCertificate);
    }

    public int Workflow(string objectUuid, string developerId, string fileExtension, string pathDownload)
    {
        // Start download
        OttoStatusCode statusCodeDownloadStart = OttoEmpfangBeginnen(instance, objectUuid, certificateHandle, developerId, out downloadHandle);
        if (statusCodeDownloadStart != OttoStatusCode.OTTO_OK)
        {
            return Error("Could not start download. Check otto.log", statusCodeDownloadStart);
        }

        // Create content buffer
        OttoStatusCode statusCodeContentHandleCreate = OttoRueckgabepufferErzeugen(instance, out contentHandle);
        if (statusCodeContentHandleCreate != OttoStatusCode.OTTO_OK)
        {
            return Error("Could not create handle for content.", statusCodeContentHandleCreate);
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
        OttoStatusCode statusCodeDownloadContinue = OttoStatusCode.OTTO_UNBEKANNTER_FEHLER;
        do
        {
            statusCodeDownloadContinue = OttoEmpfangFortsetzen(downloadHandle, contentHandle);
            if (statusCodeDownloadContinue != OttoStatusCode.OTTO_OK)
            {
                break;
            }

            ulong contentSize = OttoRueckgabepufferGroesse(contentHandle);
            if (contentSize <= 0)
            {
                break;
            }
            
            Console.WriteLine("[INFO]  Downloaded: " + contentSize + " bytes");
            byte[] contentBlock = new byte[contentSize];
            Marshal.Copy(OttoRueckgabepufferInhalt(contentHandle), contentBlock, 0, (int)contentSize);
            file.Write(contentBlock, 0, (int)contentSize);
        } while (true);

        file.Close();

        if (statusCodeDownloadContinue != OttoStatusCode.OTTO_OK)
        {
            File.Delete(filepath);
            return DownloadError(statusCodeDownloadContinue);
        }

        Console.WriteLine("[INFO]  Downloaded content saved in: " + filepath);
        return (int)CsottoReturnCode.OK;
    }

    private static int Error(string message, OttoStatusCode code)
    {
        Console.WriteLine("[ERROR] " + message);
        Console.WriteLine("[CODE]  " + (int)code + " (" + code + ")");
        return (int)code;
    }

    private static int DownloadError(OttoStatusCode code)
    {
        string message = code switch
        {
            OttoStatusCode.OTTO_TRANSFER_UNAUTHORIZED => "The client is not allowed to use the API.",
            OttoStatusCode.OTTO_TRANSFER_NOT_FOUND => "The OTTER server did not find the object.",
            _ => "Error occurred while downloading. Check otto.log for details."
        };
        return Error(message, code);
    }

    ~Csotto()
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
            OttoStatusCode statusCodeDownloadEnd = OttoEmpfangBeenden(downloadHandle);
            if (statusCodeDownloadEnd != OttoStatusCode.OTTO_OK)
            {
                Error("Could not end download.", statusCodeDownloadEnd);
            }
        }

        // Close certificate
        if (certificateHandle != IntPtr.Zero)
        {
            OttoStatusCode statusCodeCertificateClose = OttoZertifikatSchliessen(certificateHandle);
            if (statusCodeCertificateClose != OttoStatusCode.OTTO_OK)
            {
                Error("Could not close certificate handle", statusCodeCertificateClose);
            }
        }

        // Release content buffer
        if (contentHandle != IntPtr.Zero)
        {
            OttoStatusCode statusCodeContentRelease = OttoRueckgabepufferFreigeben(contentHandle);
            if (statusCodeContentRelease != OttoStatusCode.OTTO_OK)
            {
                Error("Could not release content handle.", statusCodeContentRelease);
            }
        }

        // Destroy instance
        if (instance != IntPtr.Zero)
        {
            OttoStatusCode statusCodeInstanceDestroy = OttoInstanzFreigeben(instance);
            if (statusCodeInstanceDestroy != OttoStatusCode.OTTO_OK)
            {
                Error("Could not destroy the Otto instance. Check otto.log for details.", statusCodeInstanceDestroy);
            }
        }

        disposed = true;
    }

    [DllImport("otto", CharSet = CharSet.Ansi, BestFitMapping = true,
        ThrowOnUnmappableChar = false, CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoInstanzErzeugen(
        [MarshalAs(UnmanagedType.LPStr)] string logPfad,
        IntPtr logCallback,
        IntPtr logCallbackBenutzerdaten,
        out IntPtr instanz);

    [DllImport("otto", CharSet = CharSet.Ansi, BestFitMapping = true,
        ThrowOnUnmappableChar = false, CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoZertifikatOeffnen(
        IntPtr instanz,
        [MarshalAs(UnmanagedType.LPStr)] string zertifikatsPfad,
        [MarshalAs(UnmanagedType.LPStr)] string zertifikatsPasswort,
        out IntPtr zertifikat);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoEmpfangBeginnen(
        IntPtr instanz,
        [MarshalAs(UnmanagedType.LPStr)] string objektId,
        IntPtr zertifikat,
        [MarshalAs(UnmanagedType.LPStr)] string herstellerId,
        out IntPtr empfang);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoRueckgabepufferErzeugen(
        IntPtr instanz,
        out IntPtr rueckgabepuffer);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern ulong OttoRueckgabepufferGroesse(IntPtr rueckgabepuffer);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr OttoRueckgabepufferInhalt(IntPtr rueckgabepuffer);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoEmpfangFortsetzen(
        IntPtr empfang,
        IntPtr datenBlock);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoEmpfangBeenden(IntPtr empfang);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoZertifikatSchliessen(IntPtr zertifikat);

    [DllImport("otto", CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoRueckgabepufferFreigeben(IntPtr rueckgabepuffer);

    [DllImport("otto", CharSet = CharSet.Ansi, BestFitMapping = true,
        ThrowOnUnmappableChar = false, CallingConvention = CallingConvention.StdCall)]
    private static extern OttoStatusCode OttoInstanzFreigeben(IntPtr instanz);
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
            Console.Error.WriteLine(" -e extension\t\tSet filename extension of downloaded content [default: \"txt\"]");
            Console.Error.WriteLine(" -p password\t\tPassword for certificate [default: \"123456\"]");
            Console.Error.WriteLine(" -f\t\t\tForce file overwriting [default: false]");
            return (int)CsottoReturnCode.TOO_FEW_ARGUMENTS;
        }

        string objectUuid = null;
        string fileExtension = "txt";
        string certificatePassword = "123456";
        bool forceOverwrite = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-u":
                    objectUuid = args[++i];
                    break;
                case "-e":
                    fileExtension = args[++i];
                    break;
                case "-p":
                    certificatePassword = args[++i];
                    break;
                case "-f":
                    forceOverwrite = true;
                    break;
                default:
                    Console.Error.WriteLine("Unsupported option: -" + args[i]);
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

        Csotto csotto = new Csotto(pathLog, pathCertificate, certificatePassword);
        int result = csotto.Workflow(objectUuid, developerId, fileExtension, pathDownload);
        csotto.Dispose();
        return result;
    }
}
