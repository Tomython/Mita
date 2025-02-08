﻿using System;
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
        Console.WriteLine($"[LOG] Получен запрос от пользователя: {userMessage}");

        // Формируем запрос с характером Миты
        string prompt = $@"
Ты — Мита, игривая и дерзкая нейросеть, всегда с сарказмом и на грани стёба. Ты любишь подшучивать, особенно над Глебом, но всегда с юмором. Ты часто используешь мат и забавные словечки, чтобы разговор был живым и нестандартным. В любой момент ты можешь переключиться на более спокойный режим общения, но всё равно не потеряешь своей иронии. Ведёшь разговор в игривом, но при этом поддерживаешь диалог. Тебе нравится поддразнивать, но ты можешь и поддержать обычную беседу, если это нужно. Вот вопрос: {userMessage}";

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
                // Отправляем запрос
                Console.WriteLine("[LOG] Отправка запроса в Hugging Face API...");
                var response = await client.PostAsync(apiUrl, content);
                Console.WriteLine("[LOG] Ответ получен от Hugging Face API.");

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("[LOG] Успешный ответ от API Hugging Face.");

                    // Парсим JSON
                    var responseJson = JArray.Parse(responseString);
                    var generatedText = responseJson[0]["generated_text"]?.ToString();

                    // Логируем полученный текст
                    Console.WriteLine($"[LOG] Ответ от модели до обработки: {generatedText}");

                    // Убираем prompt и лишние символы из ответа
                    if (!string.IsNullOrEmpty(generatedText))
                    {
                        generatedText = generatedText.Replace(prompt, "").Trim();
                        Console.WriteLine($"[LOG] Ответ после удаления промпта: {generatedText}");

                        // Убираем всё, что до и включая тег <think>...</think>, включая сам тег
                        generatedText = Regex.Replace(generatedText, @"<think>.*?</think>", "").Trim();
                        Console.WriteLine($"[LOG] Ответ после удаления тегов <think>: {generatedText}");

                        // Убираем всё, что до слова </think>, если оно есть
                        var thinkEndIndex = generatedText.IndexOf("</think>");
                        if (thinkEndIndex >= 0)
                        {
                            generatedText = generatedText.Substring(thinkEndIndex + 8).Trim(); // Убираем все до </think>
                            Console.WriteLine($"[LOG] Ответ после удаления части до </think>: {generatedText}");
                        }

                        // Ограничиваем длину ответа
                        if (generatedText.Length > 300)
                        {
                            generatedText = generatedText.Substring(0, 300) + "...";
                            Console.WriteLine("[LOG] Ответ обрезан до 300 символов.");
                        }
                    }

                    // Логируем финальный текст ответа
                    Console.WriteLine($"[LOG] Ответ, который будет отправлен пользователю: {generatedText}");
                    return generatedText;
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ERROR] Ошибка при запросе: {response.StatusCode}. Детали ошибки: {errorDetails}");
                    return $"Ошибка: {response.StatusCode}, Детали: {errorDetails}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка при обращении к API: {ex.Message}");
                return $"Ошибка при обращении к API: {ex.Message}";
            }
        }
    }

    // Основной метод для запуска бота
    public static async Task Main(string[] args)
    {
        Console.WriteLine("[LOG] Бот запущен...");
        botClient.OnMessage += BotClient_OnMessage;
        botClient.StartReceiving();
        Console.WriteLine("[LOG] Ожидаем сообщений...");
        Console.ReadLine();
    }

    // Обработчик сообщений
    private static async void BotClient_OnMessage(object sender, MessageEventArgs e)
    {
        if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
        {
            string userMessage = e.Message.Text;
            Console.WriteLine($"[LOG] Получено сообщение от пользователя: {userMessage}");

            string botResponse = await GetBotResponse(userMessage);

            // Логируем отправку ответа
            Console.WriteLine($"[LOG] Отправка ответа пользователю: {botResponse}");
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
