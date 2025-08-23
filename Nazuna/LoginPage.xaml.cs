using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using Windows.System;


namespace Nazuna;

public class Data
{
    private static string subKeyPath = @"SOFTWARE\Nazuna";

    public static void Set(string key, string value)
    {
        using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(subKeyPath);
        registryKey.SetValue(key, value, RegistryValueKind.String);
    }

    public static string Get(string key)
    {
        using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(subKeyPath);
        if (registryKey == null)
            return null;
        if (registryKey.GetValue(key) is string value)
            return value;
        return null;
    }

    public static void Delete(string key)
    {
        using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(subKeyPath);
        registryKey.DeleteValue(key, false);
    }
}


public sealed partial class LoginPage : Page
{
    MainWindow mainWindow;

    public LoginPage()
    {
        InitializeComponent();

        string username = Data.Get("Username");
        if (username != null)
            SetUsername(username);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is MainWindow window)
            mainWindow = window;

        Task.Run(async () =>
        {
            DispatcherQueue.TryEnqueue(() => mainWindow.Progress(true));
            string auth = await TryAuthAsync();
            DispatcherQueue.TryEnqueue(() => mainWindow.Progress(false));
            Debug(auth);
            if (string.IsNullOrWhiteSpace(auth) && mainWindow != null)
                DispatcherQueue.TryEnqueue(() => mainWindow.ContentFrame.Navigate(typeof(FilesPage), mainWindow, new DrillInNavigationTransitionInfo()));
        });
    }

    void Debug(object message) => MainWindow.Debug(message);

    void SetUsername(string username)
    {
        UsernameTextBox.Text = username;
    }

    string GetUsername()
    {
        return UsernameTextBox.Text;
    }

    string GetPassword()
    {
        return PasswordTextBox.Password;
    }

    void SetEnabledButton(bool enabled)
    {
        LoginButton.IsEnabled = enabled;
    }

    public void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        SetEnabledButton(!string.IsNullOrWhiteSpace(GetUsername()) && !string.IsNullOrWhiteSpace(GetPassword()));
    }

    public void OnInputChanged(object sender, RoutedEventArgs e)
    {
        SetEnabledButton(!string.IsNullOrWhiteSpace(GetUsername()) && !string.IsNullOrWhiteSpace(GetPassword()));
    }

    async Task<bool> TryLoginAsync()
    {
        bool successful = await Login();
        if (successful)
        {
            // UI スレッドで安全にナビゲート
            if (mainWindow != null)
            {
                DispatcherQueue.TryEnqueue(() => mainWindow.ContentFrame.Navigate(typeof(FilesPage), mainWindow, new DrillInNavigationTransitionInfo()));
            }
            Data.Set("Username", GetUsername());
        }
        return successful;
    }

    public async void Login(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            mainWindow.Progress(true);
            try
            {
                await TryLoginAsync();
            }
            catch (Exception ex)
            {
                await ShowError($"Login error: {ex.Message}");
            }
            mainWindow.Progress(false);
        }
    }

    public async void Login(object sender, RoutedEventArgs e)
    {
        mainWindow.Progress(true);
        try
        {
            await TryLoginAsync();
        }
        catch (Exception ex)
        {
            await ShowError($"Login error: {ex.Message}");
        }
        mainWindow.Progress(false);
    }

    async Task<string> TryAuthAsync()
    {
        try
        {
            string auth = await Auth();
            return auth;
        }
        catch (Exception ex)
        {
            return $"Auth error: {ex.Message}";
        }
    }

    async Task<ContentDialogResult> ShowError(string message)
    {
        return await Dialog.ShowError(Content, message);
    }

    async Task<ContentDialogResult> Show(string message)
    {
        return await Dialog.Show(Content, message);
    }

    public async Task<string> Auth()
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
                // await ShowError($"Auth failed: {response.StatusCode} - {response.ReasonPhrase}");
                // return false;
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return null;

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

            return null;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> Login()
    {
        string username = GetUsername();
        string password = GetPassword();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            string url = "https://b3ht4qmsuk.execute-api.ap-south-1.amazonaws.com/upload/authenticate";

            using HttpClient client = new();
            string json = JsonSerializer.Serialize(new { username, password });

            using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                await ShowError($"Login failed: {response.StatusCode} - {response.ReasonPhrase}");
                return false;
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(responseJson);

            // string prettyJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            // await Dialog.Show(Content, prettyJson);

            if (doc.RootElement.TryGetProperty("message", out JsonElement message))
            {
                if (message.GetString() == "Authentication failed")
                {
                    await ShowError("認証に失敗しました：ユーザー名またはパスワードが正しくありません\nAuthentication failed: Username or password is incorrect");
                    return false;
                }
                else if (message.GetString() == "Authentication successful")
                {
                    // await Show("認証に成功しました\nAuthentication successful");
                }
            }

            if (doc.RootElement.TryGetProperty("access_token", out JsonElement tokenElem))
            {
                string token = tokenElem.GetString();
                if (!string.IsNullOrEmpty(token))
                {
                    Data.Set("Token", token);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            await ShowError($"Error: {ex.Message}");
            return false;
        }
    }
}