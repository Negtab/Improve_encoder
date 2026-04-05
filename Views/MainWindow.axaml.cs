using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ImropveCrypto.Models;
using MsBox.Avalonia;




namespace ImropveCrypto.Views;

public partial class MainWindow : Window
{
    private MemoryStream? _encryptedStream;   

    public MainWindow()
    {
        InitializeComponent();
        ProgressBar.Value = 0;
        EncryptedFileTextBlock.Text = "";
    }

    private static string BytesToBitsString(byte[] data, int maxBytes = 16)
    {
        int bytesToTake = Math.Min(data.Length, maxBytes);
        var sb = new StringBuilder(bytesToTake * 8);
        
        for (int i = 0; i < bytesToTake; i++)
            sb.Append(Convert.ToString(data[i], 2).PadLeft(8, '0'));
        
        if (data.Length > maxBytes)
            sb.Append("...");
        
        return sb.ToString();
    }

    private byte[] ParseInitialState()
    {
        string text = BeginKeyTextBlock.Document?.Text ?? "";
        var bits = new List<char>();
        
        foreach (char c in text)
            if (c == '0' || c == '1')
                bits.Add(c);
        
        if (bits.Count != 28)
            throw new Exception($"Начальное состояние должно содержать ровно 28 бит (0/1). Сейчас {bits.Count} бит.");

        byte[] state = new byte[28];
        for (int i = 0; i < 28; i++)
            state[i] = (byte)(bits[i] == '1' ? 1 : 0);
        return state;
    }

    private async Task ShowKeyPreview(byte[] initialState)
    {
        var tempCrypto = new Crypto(initialState);
        byte[] keyPreview = tempCrypto.GenerateKeyBytes(16); 
        string bits = BytesToBitsString(keyPreview);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            GeneratedTextBlock.Document.Text = bits;
        });
    }

    private async void OpenFileClick(object sender, RoutedEventArgs e)
    {
        var openBtn = sender as Button;
        if (openBtn != null) openBtn.IsEnabled = false;
        SaveFileButton.IsEnabled = false;

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open file",
                AllowMultiple = false
            });
            if (files.Count == 0) return;
            
            byte[] initialState;
            try
            {
                initialState = ParseInitialState();
            }
            catch (Exception ex)
            {
                await ShowError(ex.Message);
                return;
            }

            await ShowKeyPreview(initialState);

            var crypto = new Crypto(initialState);

            await using var fileStream = await files[0].OpenReadAsync();
            long fileLength = fileStream.Length;
            _encryptedStream = new MemoryStream((int)fileLength); 

            const int bufferSize = 8192; 
            byte[] dataBuffer = new byte[bufferSize];
            byte[] encryptedBuffer = new byte[bufferSize];

            long totalBytesRead = 0;
            int bytesRead;
            bool firstBlock = true;
            byte[]? firstOriginalBytes = null;
            byte[]? firstEncryptedBytes = null;

            ProgressBar.Value = 0;

            while ((bytesRead = await fileStream.ReadAsync(dataBuffer, 0, bufferSize)) > 0)
            {
                byte[] keyBlock = crypto.GenerateKeyBytes(bytesRead);

                for (int i = 0; i < bytesRead; i++)
                    encryptedBuffer[i] = (byte)(dataBuffer[i] ^ keyBlock[i]);

                await _encryptedStream.WriteAsync(encryptedBuffer, 0, bytesRead);

                if (firstBlock)
                {
                    firstOriginalBytes = dataBuffer.Take(Math.Min(16, bytesRead)).ToArray();
                    firstEncryptedBytes = encryptedBuffer.Take(Math.Min(16, bytesRead)).ToArray();
                    firstBlock = false;
                }

                totalBytesRead += bytesRead;
                double progress = (double)totalBytesRead / fileLength * 100;
                await Dispatcher.UIThread.InvokeAsync(() => ProgressBar.Value = progress);
            }

            string originalBits = firstOriginalBytes != null
                ? BytesToBitsString(firstOriginalBytes)
                : "";
            string encryptedBits = firstEncryptedBytes != null
                ? BytesToBitsString(firstEncryptedBytes)
                : "";

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                FileTextBlock.Document.Text = originalBits;
                EncryptedTextEditor.Document.Text = encryptedBits;
                ProgressBar.Value = 100;
                EncryptedFileTextBlock.Text = $"Файл зашифрован. Размер: {totalBytesRead} байт";
            });
        }
        catch (Exception ex)
        {
            await ShowError($"Ошибка: {ex.Message}");
        }
        finally
        {
            if (openBtn != null) openBtn.IsEnabled = true;
            SaveFileButton.IsEnabled = true;
        }
    }

    private async void SaveFileClick(object sender, RoutedEventArgs e)
    {
        if (_encryptedStream == null || _encryptedStream.Length == 0)
        {
            await ShowError("Нет зашифрованных данных. Сначала откройте файл.");
            return;
        }

        var saveBtn = sender as Button;
        if (saveBtn != null) saveBtn.IsEnabled = false;

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save encrypted file",
                SuggestedFileName = "encrypted.bin"
            });
            if (file == null) return;

            await using var destStream = await file.OpenWriteAsync();
            _encryptedStream.Position = 0; // в начало потока
            await _encryptedStream.CopyToAsync(destStream);
            await destStream.FlushAsync();

            await ShowMessage("Файл успешно сохранён.");
        }
        catch (Exception ex)
        {
            await ShowError($"Ошибка сохранения: {ex.Message}");
        }
        finally
        {
            if (saveBtn != null) saveBtn.IsEnabled = true;
        }
    }

    private async Task ShowError(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Ошибка", message);
            await box.ShowWindowDialogAsync(this);
        });
    }

    private async Task ShowMessage(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Информация", message);
            await box.ShowWindowDialogAsync(this);
        });
    }
}