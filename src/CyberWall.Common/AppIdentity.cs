using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CyberWall.Common;

public sealed record AppIdentityInfo(
    string FileName,
    string ProductName,
    string? Publisher,
    bool IsSigned,
    bool IsMicrosoft,
    bool IsSystemPath);

public static class AppIdentity
{
    private static readonly ConcurrentDictionary<string, AppIdentityInfo> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static AppIdentityInfo Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new AppIdentityInfo("unknown", "unknown", null, false, false, false);

        try
        {
            return Cache.GetOrAdd(path, ResolveCore);
        }
        catch
        {
            var fn = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fn)) fn = path;
            return new AppIdentityInfo(fn, fn, null, false, false, false);
        }
    }

    private static AppIdentityInfo ResolveCore(string path)
    {
        try
        {
            return ResolveCoreUnsafe(path);
        }
        catch
        {
            var fn = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fn)) fn = path;
            return new AppIdentityInfo(fn, fn, null, false, false, false);
        }
    }

    private static AppIdentityInfo ResolveCoreUnsafe(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(fileName)) fileName = path;

        string? description = null;
        string? product = null;
        if (File.Exists(path))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                description = Clean(info.FileDescription);
                product = Clean(info.ProductName);
            }
            catch { }
        }

        var (signed, publisher) = TryExtractSigner(path);

        var systemPath = IsWindowsSystemPath(path);
        var microsoft = systemPath
            || ContainsMicrosoft(publisher)
            || ContainsMicrosoft(product);

        var hero = PickHero(fileName, description, product);
        return new AppIdentityInfo(fileName, hero, publisher, signed, microsoft, systemPath);
    }

    private static (bool isSigned, string? publisher) TryExtractSigner(string path)
    {
        if (!File.Exists(path)) return (false, null);

        IntPtr hCertStore = IntPtr.Zero;
        IntPtr hMsg = IntPtr.Zero;
        IntPtr pCertContext = IntPtr.Zero;
        IntPtr pSignerCertInfo = IntPtr.Zero;

        try
        {
            bool ok = CryptQueryObject(
                CERT_QUERY_OBJECT_FILE,
                path,
                CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                CERT_QUERY_FORMAT_FLAG_ALL,
                0,
                out _, out _, out _,
                out hCertStore,
                out hMsg,
                out _);

            if (!ok || hMsg == IntPtr.Zero || hCertStore == IntPtr.Zero)
                return (false, null);

            int cbData = 0;
            if (!CryptMsgGetParam(hMsg, CMSG_SIGNER_CERT_INFO_PARAM, 0, IntPtr.Zero, ref cbData) || cbData <= 0)
                return (false, null);

            pSignerCertInfo = Marshal.AllocHGlobal(cbData);
            if (!CryptMsgGetParam(hMsg, CMSG_SIGNER_CERT_INFO_PARAM, 0, pSignerCertInfo, ref cbData))
                return (false, null);

            pCertContext = CertFindCertificateInStore(
                hCertStore,
                X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
                0,
                CERT_FIND_CERT_INFO,
                pSignerCertInfo,
                IntPtr.Zero);

            if (pCertContext == IntPtr.Zero)
                return (false, null);

            var sb = new StringBuilder(256);
            int len = CertGetNameString(pCertContext, CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, IntPtr.Zero, sb, sb.Capacity);
            if (len > 1)
            {
                var pub = Clean(sb.ToString());
                return (!string.IsNullOrEmpty(pub), pub);
            }

            return (true, null);
        }
        catch
        {
            return (false, null);
        }
        finally
        {
            if (pSignerCertInfo != IntPtr.Zero) Marshal.FreeHGlobal(pSignerCertInfo);
            if (pCertContext != IntPtr.Zero) CertFreeCertificateContext(pCertContext);
            if (hCertStore != IntPtr.Zero) CertCloseStore(hCertStore, 0);
            if (hMsg != IntPtr.Zero) CryptMsgClose(hMsg);
        }
    }

    private static string PickHero(string fileName, string? description, string? product)
    {
        if (fileName.Equals("git-remote-https.exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("git-remote-http.exe", StringComparison.OrdinalIgnoreCase))
        {
            return $"Git ({fileName})";
        }

        if (!string.IsNullOrEmpty(description) &&
            !description.Equals(fileName, StringComparison.OrdinalIgnoreCase) &&
            !description.Equals(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        if (!string.IsNullOrEmpty(product) && !IsGenericProduct(product) &&
            !product.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        {
            return product;
        }

        return fileName;
    }

    private static bool IsGenericProduct(string product) =>
        product.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
        product.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) ||
        product.Equals("Microsoft Windows Operating System", StringComparison.OrdinalIgnoreCase) ||
        product.Equals("Microsoft® Windows® Operating System", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsMicrosoft(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

    private static bool IsWindowsSystemPath(string path)
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windows) && path.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }
        return path.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\Windows\SysWOW64\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\Windows\WinSxS\", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    #region Win32 Crypto P/Invoke
    private const int CERT_QUERY_OBJECT_FILE = 1;
    private const int CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED = 1 << 10;
    private const int CERT_QUERY_FORMAT_FLAG_ALL = 0x0E;
    private const int CMSG_SIGNER_CERT_INFO_PARAM = 7;
    private const int X509_ASN_ENCODING = 0x00000001;
    private const int PKCS_7_ASN_ENCODING = 0x00010000;
    private const int CERT_FIND_CERT_INFO = 11 << 16;
    private const int CERT_NAME_SIMPLE_DISPLAY_TYPE = 4;

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptQueryObject(
        int dwObjectType,
        [MarshalAs(UnmanagedType.LPWStr)] string pvObject,
        int dwExpectedContentTypeFlags,
        int dwExpectedFormatTypeFlags,
        int dwFlags,
        out int pdwMsgAndCertEncodingType,
        out int pdwContentType,
        out int pdwFormatType,
        out IntPtr phCertStore,
        out IntPtr phMsg,
        out IntPtr ppvContext);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptMsgGetParam(
        IntPtr hCryptMsg,
        int dwParamType,
        int dwIndex,
        IntPtr pvData,
        ref int pcbData);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern IntPtr CertFindCertificateInStore(
        IntPtr hCertStore,
        int dwCertEncodingType,
        int dwFindFlags,
        int dwFindType,
        IntPtr pvFindPara,
        IntPtr pPrevCertContext);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CertGetNameString(
        IntPtr pCertContext,
        int dwType,
        int dwFlags,
        IntPtr pvTypePara,
        StringBuilder pszNameString,
        int cchNameString);

    [DllImport("crypt32.dll")]
    private static extern bool CertCloseStore(IntPtr hCertStore, int dwFlags);

    [DllImport("crypt32.dll")]
    private static extern bool CryptMsgClose(IntPtr hCryptMsg);

    [DllImport("crypt32.dll")]
    private static extern bool CertFreeCertificateContext(IntPtr pCertContext);
    #endregion
}
