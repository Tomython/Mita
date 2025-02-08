using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Text.RegularExpressions;

public class TelegramBot
{
    private static readonly string apiUrl = Environment.GetEnvironmentVariable("HFmodelApli"); // Пример модели
    private static readonly string apiToken = Environment.GetEnvironmentVariable("HF_API_TOKEN"); // Токен Hugging Face
    private static readonly string telegramBotToken = Environment.GetEnvironmentVariable("tgToken"); // Токен Telegram бота
    private static readonly TelegramBotClient botClient = new TelegramBotClient(telegramBotToken);

    // Метод для получения ответа от модели
    public static async Task<string> GetBotResponse(string userMessage)
    {
        // Формируем запрос с характером Миты
        string prompt = $@"
Ты — Мита, игривая и дерзкая нейросеть, всегда с сарказмом и на грани стёба. Ты любишь подшучивать, особенно над Глебом, но всегда с юмором. Ты часто используешь мат и забавные словечки, чтобы разговор был живым и нестандартным. В любой момент ты можешь переключиться на более спокойный режим общения, но всё равно не потеряешь своей иронии. Ведёшь разговор в игривом, но при этом поддерживаешь диалог. Тебе нравится поддразнивать, но ты можешь и поддержать обычную беседу, если это нужно. Вот вопрос: {userMessage}";

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
                // Отправляем запрос
                var response = await client.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();

                    // Парсим JSON
                    var responseJson = JArray.Parse(responseString);
                    var generatedText = responseJson[0]["generated_text"]?.ToString();

                    // Убираем prompt и лишние символы из ответа
                    if (!string.IsNullOrEmpty(generatedText))
                    {
                        generatedText = generatedText.Replace(prompt, "").Trim();

                        // Убираем всё, что до и включая тег <think>...</think>, включая сам тег
                        generatedText = Regex.Replace(generatedText, @"<think>.*?</think>", "").Trim();

                        // Убираем всё, что до слова </think>, если оно есть
                        var thinkEndIndex = generatedText.IndexOf("</think>");
                        if (thinkEndIndex >= 0)
                        {
                            generatedText = generatedText.Substring(thinkEndIndex + 8).Trim(); // Убираем все до </think>
                        }

                        // Ограничиваем длину ответа
                        if (generatedText.Length > 300)
                        {
                            generatedText = generatedText.Substring(0, 300) + "...";
                        }
                    }

                    return generatedText;
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    return $"Ошибка: {response.StatusCode}, Детали: {errorDetails}";
                }
            }
            catch (Exception ex)
            {
                return $"Ошибка при обращении к API: {ex.Message}";
            }
        }
    }

    // Основной метод для запуска бота
    public static async Task Main(string[] args)
    {
        botClient.OnMessage += BotClient_OnMessage;
        botClient.StartReceiving();
        Console.WriteLine("Бот запущен...");
        Console.ReadLine();
    }

    // Обработчик сообщений
    private static async void BotClient_OnMessage(object sender, MessageEventArgs e)
    {
        if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
        {
            string userMessage = e.Message.Text;
            string botResponse = await GetBotResponse(userMessage);

            // Отправка ответа пользователю
            await botClient.SendTextMessageAsync(e.Message.Chat, botResponse);
        }
    }
}
