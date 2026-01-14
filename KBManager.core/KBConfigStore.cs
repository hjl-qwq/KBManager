using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// Config file manager
/// </summary>
/// <typeparam name="T">Type of config data</typeparam>
public class CrossPlatformConfig<T> where T : new()
{
    private readonly string _configFileName = "config.json";
    private readonly string _configFilePath;

    /// <summary>
    /// Initialize manager
    /// </summary>
    /// <param name="appName">Name of application</param>
    public CrossPlatformConfig(string appName)
    {
        string configRootDir = GetConfigRootDirectory();

        string appConfigDir = Path.Combine(configRootDir, appName);

        if (!Directory.Exists(appConfigDir))
        {
            Directory.CreateDirectory(appConfigDir);
        }

        _configFilePath = Path.Combine(appConfigDir, _configFileName);
    }

    private string GetConfigRootDirectory()
    {
        string osVersion = Environment.OSVersion.Platform.ToString();

        if (osVersion.Contains("Win32") || osVersion.Contains("Win64"))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (osVersion == "Unix" || osVersion == "Linux")
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".config");
        }
        else
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }

    public T ReadConfig()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                return new T();
            }

            string jsonContent = File.ReadAllText(_configFilePath);
            T config = JsonSerializer.Deserialize<T>(jsonContent);
            return config != null ? config : new T();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get config file: {ex.Message}");
            return new T();
        }
    }

    public void WriteConfig(T configData)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string jsonContent = JsonSerializer.Serialize(configData, jsonOptions);
            File.WriteAllText(_configFilePath, jsonContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write config file: {ex.Message}");
        }
    }

    public string GetConfigFilePath()
    {
        return _configFilePath;
    }
}