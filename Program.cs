using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Text.RegularExpressions;

public class TelegramBot
{
    private static readonly string apiUrl = Environment.GetEnvironmentVariable("HFmodelApli"); // API нейросети
    private static readonly string apiToken = Environment.GetEnvironmentVariable("HF_API_TOKEN"); // Токен Hugging Face
    private static readonly string telegramBotToken = Environment.GetEnvironmentVariable("tgToken"); // Токен Telegram бота
    private static readonly TelegramBotClient botClient = new TelegramBotClient(telegramBotToken);

    // Метод для получения ответа от модели
    public static async Task<string> GetBotResponse(string userMessage)
    {
        Console.WriteLine($"[LOG] Получен запрос от пользователя: {userMessage}");

        string prompt = $@"{userMessage}";

        Console.WriteLine($"[LOG] Промпт, отправляемый в модель: {prompt}");

        var requestData = new
        {
            inputs = prompt,
            options = new { wait_for_model = true }
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

            try
            {
                Console.WriteLine("[LOG] Отправка запроса в Hugging Face API...");
                var response = await client.PostAsync(apiUrl, content);
                Console.WriteLine("[LOG] Ответ получен от Hugging Face API.");

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var responseJson = JArray.Parse(responseString);
                    var generatedText = responseJson[0]["generated_text"]?.ToString();

                    // Очищаем ответ от лишнего
                    if (!string.IsNullOrEmpty(generatedText))
                    {
                        generatedText = generatedText.Replace(prompt, "").Trim();
                        generatedText = Regex.Replace(generatedText, @"<think>.*?</think>", "").Trim();
                        int thinkEndIndex = generatedText.IndexOf("</think>");
                        if (thinkEndIndex >= 0)
                        {
                            generatedText = generatedText.Substring(thinkEndIndex + 8).Trim();
                        }

                        if (generatedText.Length > 1000)
                        {
                            generatedText = generatedText.Substring(0, 1000) + "...";
                        }
                    }

                    return generatedText;
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    return $"Ошибка API: {response.StatusCode}, Детали: {errorDetails}";
                }
            }
            catch (Exception ex)
            {
                return $"Ошибка при обращении к API: {ex.Message}";
            }
        }
    }

    // Метод для запуска HTTP-сервера
    public static async Task StartHttpServer()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://*:941/");
        listener.Start();
        Console.WriteLine("[LOG] HTTP-сервер запущен на порту 941...");

        while (true)
        {
            HttpListenerContext context = await listener.GetContextAsync();
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/mitabot")
            {
                using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string requestBody = await reader.ReadToEndAsync();
                    dynamic json = JObject.Parse(requestBody);
                    string userMessage = json.message.ToString();
                    Console.WriteLine($"[LOG] Запрос на HTTP-сервере: {userMessage}");

                    string botResponse = await GetBotResponse(userMessage);

                    byte[] responseBytes = Encoding.UTF8.GetBytes(botResponse);
                    response.ContentLength64 = responseBytes.Length;
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
            }

            response.Close();
        }
    }

    // Основной метод
    public static async Task Main(string[] args)
    {
        Console.WriteLine("[LOG] Бот запущен...");
        botClient.OnMessage += BotClient_OnMessage;
        botClient.StartReceiving();
        Console.WriteLine("[LOG] Ожидаем сообщений...");

        // Запускаем HTTP-сервер
        await StartHttpServer();
    }

    // Обработчик сообщений в Telegram
    private static async void BotClient_OnMessage(object sender, MessageEventArgs e)
    {
        if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
        {
            string userMessage = e.Message.Text;
            Console.WriteLine($"[LOG] Получено сообщение от пользователя: {userMessage}");

            string botResponse = await GetBotResponse(userMessage);

            try
            {
                await botClient.SendTextMessageAsync(e.Message.Chat, botResponse);
                Console.WriteLine("[LOG] Ответ успешно отправлен пользователю.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка при отправке сообщения в Telegram: {ex.Message}");
            }
        }
    }
}
