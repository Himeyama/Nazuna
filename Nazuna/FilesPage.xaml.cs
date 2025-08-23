using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;


namespace Nazuna;

public class FileInfo
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }
    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }

    public string FileSize
    {
        get
        {
            long size = Size;
            string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
            int unit = 0;
            double displaySize = size;

            while (displaySize >= 1024 && unit < units.Length - 1)
            {
                displaySize /= 1024;
                unit++;
            }

            return $"{displaySize:0.##} {units[unit]}";
        }
    }

    public string LastModifiedText
    {
        get
        {
            return LastModified.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}

public sealed partial class FilesPage : Page
{
    MainWindow mainWindow;

    public FilesPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is MainWindow window)
            mainWindow = window;

        _ = GetFiles();
    }

    // SHGetKnownFolderPath 関数のインポート
    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);

    // ダウンロードフォルダの GUID
    private static readonly Guid FolderDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>
    /// Known Folder のパスを取得する汎用関数
    /// </summary>
    public static string GetKnownFolderPath(Guid knownFolderId)
    {
        IntPtr outPath;
        int result = SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out outPath);
        if (result != 0) // 0以外はエラー
        {
            throw new ExternalException("SHGetKnownFolderPath failed", result);
        }

        string path = Marshal.PtrToStringUni(outPath);
        Marshal.FreeCoTaskMem(outPath);
        return path;
    }

    void Logout(object sender, RoutedEventArgs e)
    {
        Data.Delete("Token");
        mainWindow.ContentFrame.Navigate(typeof(LoginPage), mainWindow, new DrillInNavigationTransitionInfo());
    }

    public async Task<string> GetFiles()
    {
        string username = Data.Get("Username");
        string token = Data.Get("Token");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
        {
            return "ユーザー名またはトークンが設定されていません\nUsername or token is not set";
        }

        try
        {
            string url = $"https://b3ht4qmsuk.execute-api.ap-south-1.amazonaws.com/upload/getFiles?username={username}&token={token}";

            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return $"Auth failed: {response.StatusCode} - {response.ReasonPhrase}";
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                List<FileInfo> files = JsonSerializer.Deserialize<List<FileInfo>>(responseJson);
                Files.Items.Clear();
                if (files != null)
                {
                    foreach (FileInfo file in files)
                    {
                        if (string.IsNullOrWhiteSpace(file.FileName))
                            continue;
                        Files.Items.Add(file);
                    }
                }
                return null;
            }

            if (doc.RootElement.TryGetProperty("error", out JsonElement error))
            {
                if (error.GetString() == "Invalid token or expired")
                {
                    return "認証に失敗しました：トークンが無効です\nAuthentication failed: Invalid token";
                }
                else if (error.GetString() == "Authentication required")
                {
                    return "認証が必要です\nAuthentication successful";
                }

                return $"認証に失敗しました\nAuthentication successful\n\n{error.GetString()}";
            }

            return "Error: 不明なエラー";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    async Task<string> GetDownloadLink(string fileName)
    {
        string token = Data.Get("Token");
        string username = Data.Get("Username");

        string url = $"https://b3ht4qmsuk.execute-api.ap-south-1.amazonaws.com/upload/getFile?username={username}&token={token}&filename={fileName}";
        using HttpClient client = new();

        try
        {
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("presigned_url", out JsonElement presignedUrlElement))
            {
                string presignedUrl = presignedUrlElement.GetString();
                Debug($"Presigned URL: {presignedUrl}");
                return presignedUrl;
            }
            else
            {
                Debug("presigned_url not found in response.");
            }
        }
        catch (Exception ex)
        {
            Debug(ex.Message);
        }
        return null;
    }

    async void Download(object sender, RoutedEventArgs e)
    {
        if (Files.SelectedItem is FileInfo fileInfo)
        {
            string fileName = fileInfo.FileName;
            string downloadLink = await GetDownloadLink(fileName);
            if (downloadLink != null)
            {
                try
                {
                    string downloadsPath = GetKnownFolderPath(FolderDownloads);
                    string savePath = Path.Combine(downloadsPath, fileName);

                    using HttpClient httpClient = new();
                    using HttpResponseMessage response = await httpClient.GetAsync(downloadLink);
                    response.EnsureSuccessStatusCode();

                    await using Stream contentStream = await response.Content.ReadAsStreamAsync();
                    await using FileStream fileStream = File.Create(savePath);
                    await contentStream.CopyToAsync(fileStream);

                    Debug($"File saved: {savePath}");
                }
                catch (Exception ex)
                {
                    Debug($"Download error: {ex.Message}");
                }
            }
            else
            {
                Debug("Failed to get download link.");
            }
        }
    }

    async void Delete(object sender, RoutedEventArgs e)
    {
        string token = Data.Get("Token");
        string username = Data.Get("Username");

        if (Files.SelectedItem is FileInfo fileInfo)
        {
            string fileName = fileInfo.FileName;
            string url = $"https://b3ht4qmsuk.execute-api.ap-south-1.amazonaws.com/upload/deleteFile?username={username}&token={token}&filename={fileName}";
            using HttpClient client = new();
            try
            {
                HttpRequestMessage request = new(HttpMethod.Delete, url);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    Debug($"Deleted: {fileName}");
                    await GetFiles();
                }
                else
                {
                    Debug($"Delete failed: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Debug($"Delete error: {ex.Message}");
            }
        }
    }

    async Task<string> GetUploadUrl(string fileName)
    {
        string username = Data.Get("Username");
        string token = Data.Get("Token");
        string url = "https://b3ht4qmsuk.execute-api.ap-south-1.amazonaws.com/upload/upload";

        using HttpClient client = new();
        var body = new { username, token, fileName };
        string json = JsonSerializer.Serialize(body);

        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, content);
            string responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                Debug(response);
                using JsonDocument doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("uploadUrl", out JsonElement uploadUrl))
                {
                    string uploadUrlText = uploadUrl.GetString();
                    Debug($"RequestUri: {uploadUrlText}");
                    return uploadUrlText;
                }
            }
            else
            {
                Debug($"Error: {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            Debug($"Exception: {ex.Message}");
        }
        return null;
    }

    async void Upload(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            FileTypeFilter = { "*" }
        };
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(mainWindow));

        StorageFile file = await picker.PickSingleFileAsync();
        if (file == null)
        {
            Debug("No file was selected");
            return;
        }

        string uploadUrl = await GetUploadUrl(file.Name);
        Debug(uploadUrl);
        if (uploadUrl != null)
        {
            try
            {
                using Stream fileStream = await file.OpenStreamForReadAsync();
                using HttpClient httpClient = new();
                using HttpRequestMessage request = new(HttpMethod.Put, uploadUrl)
                {
                    Content = new StreamContent(fileStream)
                };
                // オプション: 必要に応じて Content-Type を設定
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                HttpResponseMessage response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    Debug("Upload successful");
                    await GetFiles();
                }
                else
                {
                    Debug($"Upload failed: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Debug($"Upload error: {ex.Message}");
            }
        }
        else
        {
            Debug("Failed to get upload URL.");
        }
    }

    void Clicked(object sender, RoutedEventArgs e)
    {
        DownloadButton.IsEnabled = true;
        DeleteButton.IsEnabled = true;
    }
    
    void Debug(object message) => MainWindow.Debug(message);
}