using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace ClippyWpf
{
    public class ChatMessage
    {
        private string _text = "";
        public string Text 
        { 
            get => _text; 
            set => _text = Regex.Replace(value ?? "", @"\*+", ""); 
        }
        public string ImagePath { get; set; } = "";
        public bool IsUser { get; set; } = false;
        public Visibility TextVisibility => string.IsNullOrEmpty(Text) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ImageVisibility => string.IsNullOrEmpty(ImagePath) ? Visibility.Collapsed : Visibility.Visible;
    }

    public partial class MainWindow : Window
    {
        private const string ApiKey = "вставьте_сюда_ваш_ключ_api";
        private static readonly HttpClient client = new HttpClient();
        private static readonly string HistoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
        
        private bool isChatVisible = true;
        private bool isDraggingWithRightButton = false;
        private Point startMousePosition;

        private const string SystemPrompt = 
    "Ты — Clippy, продвинутый и крутой ИИ-помощник. Твои правила:\n" +
    "1. Ты общаешься в свободном, дружеском и непринужденном стиле, но без смайликов и эмодзи,и общайся русским языком(только текст).\n" +
    "2. Не нужно представляться каждый раз и писать банальные приветствия про Microsoft Office и шаги для Word.\n" +
    "3. Отвечай сразу и четко по делу на любой вопрос пользователя (будь то программирование, железо или просто общение).\n" +
    "4. Если тебя просят написать код или инструкцию — делай это структурированно, но без занудства.";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void ChatWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void ChatWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ChatBubbleList != null)
            {
                var border = VisualTreeHelper.GetChild(ChatBubbleList, 0) as Border;
                if (border != null)
                {
                    var scrollViewer = border.Child as ScrollViewer;
                    if (scrollViewer != null)
                    {
                        if (e.Delta > 0) scrollViewer.LineUp();
                        else scrollViewer.LineDown();
                        e.Handled = true;
                    }
                }
            }
        }

        private void ClippyCharacter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; 
            double toOpacity = isChatVisible ? 0.0 : 1.0;
            isChatVisible = !isChatVisible;

            var anim = new DoubleAnimation { To = toOpacity, Duration = TimeSpan.FromSeconds(0.3) };
            if (isChatVisible) ChatWindow.IsHitTestVisible = true;
            else anim.Completed += (s, a) => { if (!isChatVisible) ChatWindow.IsHitTestVisible = false; };

            ChatWindow.BeginAnimation(Grid.OpacityProperty, anim);
        }

        private void ClippyCharacter_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.RightButton == MouseButtonState.Pressed)
            {
                isDraggingWithRightButton = true;
                startMousePosition = e.GetPosition(this);
                ClippyCharacter.CaptureMouse(); 
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDraggingWithRightButton && e.RightButton == MouseButtonState.Pressed)
            {
                Point currentMousePosition = e.GetPosition(this);
                double deltaX = currentMousePosition.X - startMousePosition.X;
                double deltaY = currentMousePosition.Y - startMousePosition.Y;
                this.Left += deltaX;
                this.Top += deltaY;
            }
            else if (isDraggingWithRightButton && e.RightButton == MouseButtonState.Released)
            {
                EndRightButtonDrag();
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) { base.OnMouseRightButtonUp(e); EndRightButtonDrag(); }
        private void EndRightButtonDrag() { if (isDraggingWithRightButton) { isDraggingWithRightButton = false; ClippyCharacter.ReleaseMouseCapture(); } }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => this.Close();

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(HistoryPath)) { try { File.Delete(HistoryPath); } catch { } }
            ChatBubbleList.Items.Clear();
            AddMessageToChat("Привет! Я ИИ-помощник Clippy. История чата очищена. Введите ваш запрос в поле ниже.", false);
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (sender is ListBoxItem item && item.DataContext is ChatMessage message) { if (!string.IsNullOrEmpty(message.Text)) Clipboard.SetText(message.Text); } }
        
        private void SaveHistory() 
        { 
            try 
            { 
                var messages = new List<ChatMessage>(); 
                foreach (var item in ChatBubbleList.Items) 
                { 
                    if (item is ChatMessage msg && msg.Text != null && !msg.Text.StartsWith("Скрепка формирует")) messages.Add(msg); 
                } 
                string json = JsonConvert.SerializeObject(messages, Formatting.Indented); 
                File.WriteAllText(HistoryPath, json, Encoding.UTF8); 
            } 
            catch { } 
        }

        private void LoadHistory() 
        { 
            ChatBubbleList.Items.Clear(); 
            if (File.Exists(HistoryPath)) 
            { 
                try 
                { 
                    string json = File.ReadAllText(HistoryPath, Encoding.UTF8); 
                    var messages = JsonConvert.DeserializeObject<List<ChatMessage>>(json); 
                    if (messages != null) 
                    { 
                        foreach (var msg in messages) ChatBubbleList.Items.Add(msg); 
                    } 
                } 
                catch { } 
            } 
            if (ChatBubbleList.Items.Count == 0) AddMessageToChat("Привет! Я ИИ-помощник Clippy. Введите ваш запрос в поле ниже.", false); 
            else ScrollToBottom(); 
        }

        private void AddMessageToChat(string text, bool isUser, string imagePath = "") { var msg = new ChatMessage { Text = text, IsUser = isUser, ImagePath = imagePath }; ChatBubbleList.Items.Add(msg); ScrollToBottom(); SaveHistory(); }
        private void ScrollToBottom() { if (ChatBubbleList.Items.Count > 0) { ChatBubbleList.UpdateLayout(); var lastItem = ChatBubbleList.Items[ChatBubbleList.Items.Count - 1]; ChatBubbleList.ScrollIntoView(lastItem); } }
        private async void Entry_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { if (Clipboard.ContainsImage()) { e.Handled = true; await ProcessClipboardImage(); return; } } if (e.Key == Key.Enter) { e.Handled = true; string userText = EntryField.Text.Trim(); if (string.IsNullOrEmpty(userText)) return; AddMessageToChat(userText, true); EntryField.Clear(); await ProcessAiResponse(); } }
        private async void SendBtn_Click(object sender, RoutedEventArgs e) { string userText = EntryField.Text.Trim(); if (string.IsNullOrEmpty(userText)) return; AddMessageToChat(userText, true); EntryField.Clear(); await ProcessAiResponse(); }
        private async void EntryField_Pasting(object sender, DataObjectPastingEventArgs e) { if (Clipboard.ContainsImage()) { e.CancelCommand(); await ProcessClipboardImage(); } }
        private async Task ProcessClipboardImage() { try { BitmapSource image = Clipboard.GetImage(); if (image == null) return; string tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_images"); Directory.CreateDirectory(tempFolder); string filePath = Path.Combine(tempFolder, $"{Guid.NewGuid()}.png"); using (var fileStream = new FileStream(filePath, FileMode.Create)) { BitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(fileStream); } string userText = EntryField.Text.Trim(); EntryField.Clear(); AddMessageToChat(userText, true, filePath); await ProcessAiResponse(); } catch (Exception ex) { MessageBox.Show("Не удалось вставить изображение: " + ex.Message); } }
        private async void AttachBtn_Click(object sender, RoutedEventArgs e) { OpenFileDialog openFileDialog = new OpenFileDialog(); openFileDialog.Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Все файлы (*.*)|*.*"; if (openFileDialog.ShowDialog() == true) { string userText = EntryField.Text.Trim(); EntryField.Clear(); AddMessageToChat(userText, true, openFileDialog.FileName); await ProcessAiResponse(); } }
        
        private async Task ProcessAiResponse() 
        { 
            var loadingMsg = new ChatMessage { Text = "Скрепка формирует ответ...", IsUser = false }; 
            ChatBubbleList.Items.Add(loadingMsg); 
            ScrollToBottom(); 
            string reply = await SendToAi(); 
            ChatBubbleList.Items.Remove(loadingMsg); 
            AddMessageToChat(reply, false); 
        }

        private async Task<string> SendToAi() { string reply = await ExecuteApiRequest("google/gemini-2.5-flash:free"); if (reply.StartsWith("Ошибка:")) reply = await ExecuteApiRequest("openrouter/auto"); return reply; }
        private string GetBase64Image(string path) { try { if (File.Exists(path)) { byte[] imageBytes = File.ReadAllBytes(path); return Convert.ToBase64String(imageBytes); } } catch { } return ""; }
        
        private async Task<string> ExecuteApiRequest(string modelName) 
        { 
            var url = "https://openrouter.ai/api/v1/chat/completions"; 
            client.DefaultRequestHeaders.Clear(); 
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}"); 
            var apiMessages = new List<object>(); 
            apiMessages.Add(new { role = "system", content = SystemPrompt }); 
            
            foreach (var item in ChatBubbleList.Items) 
            { 
                if (item is ChatMessage msg && msg.Text != null) 
                { 
                    if (msg.Text.StartsWith("Скрепка формирует") || msg.Text.StartsWith("Ошибка:")) continue; 
                    string role = msg.IsUser ? "user" : "assistant"; 
                    if (!string.IsNullOrEmpty(msg.ImagePath)) 
                    { 
                        string base64 = GetBase64Image(msg.ImagePath); 
                        var multiContent = new List<object>(); 
                        if (!string.IsNullOrEmpty(msg.Text)) multiContent.Add(new { type = "text", text = msg.Text }); 
                        if (!string.IsNullOrEmpty(base64)) multiContent.Add(new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64}" } }); 
                        apiMessages.Add(new { role = role, content = multiContent.ToArray() }); 
                    } 
                    else 
                    { 
                        apiMessages.Add(new { role = role, content = msg.Text }); 
                    } 
                } 
            } 
            
            var payload = new { model = modelName, messages = apiMessages.ToArray() }; 
            try 
            { 
                var json = JsonConvert.SerializeObject(payload); 
                var content = new StringContent(json, Encoding.UTF8, "application/json"); 
                var response = await client.PostAsync(url, content); 
                if (!response.IsSuccessStatusCode) return "Ошибка: Сервер вернул код " + response.StatusCode; 
                var responseString = await response.Content.ReadAsStringAsync(); 
                dynamic resData = JsonConvert.DeserializeObject(responseString) ?? new object(); 
                if (resData != null && resData.choices != null && resData.choices.Count > 0) 
                { 
                    string? txt = resData.choices[0].message.content; 
                    return txt ?? "Ошибка: Сервер вернул пустой текст."; 
                } 
                return "Ошибка: Пустой ответ от сервера."; 
            } 
            catch 
            { 
                return "Ошибка: Произошла ошибка сети при запросе."; 
            } 
        }
    }
}