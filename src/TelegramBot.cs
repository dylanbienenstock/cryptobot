using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoBot.Indicators;
using Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace CryptoBot
{
    public static class TelegramBot
    {
        public class InlineKeyboard
        {
            public readonly InlineKeyboardMarkup Markup;

            public InlineKeyboard(params (string text, Action<CallbackQuery> callback)[] buttons) =>
                Markup = CreateInlineKeyboard(buttons);

            private static InlineKeyboardButton CreateInlineButton(string text, Action<CallbackQuery> callback)
            {
                string guid = Guid.NewGuid().ToString();
                _inlineReplyCallbacks[guid] = callback;
                return InlineKeyboardButton.WithCallbackData(text, guid);
            }

            private static InlineKeyboardMarkup CreateInlineKeyboard((string text, Action<CallbackQuery> callback)[] buttons)
            {
                if (buttons == null || buttons.Length == 0) return null;
                var markupButtons = buttons.Select(button => CreateInlineButton(button.text, button.callback));
                return new InlineKeyboardMarkup(markupButtons);
            }
        }

        private static string _sentMessageFile = ".telegram.sent_messages.csv";

        private static TelegramBotClient _bot;
        private static string _chatId;
        private static Dictionary<string, Action<CallbackQuery>> _inlineReplyCallbacks;

        public static void Initialize()
        {
            string botToken       = Environment.GetEnvironmentVariable("Telegram_BotToken");
            _chatId               = Environment.GetEnvironmentVariable("Telegram_ChatId");
            _inlineReplyCallbacks = new Dictionary<string, Action<CallbackQuery>>();

            _bot = new TelegramBotClient(botToken);
            _bot.OnMessage       += (_, e) => OnReceiveMessage(e.Message);
            _bot.OnCallbackQuery += (_, e) => OnInlineButtonPressed(e.CallbackQuery);
            _bot.StartReceiving(new []
            {
                UpdateType.Message,
                UpdateType.CallbackQuery
            });

            RemoveOldInlineKeyboards();
            CreateSentMessageFile();
        }

        private static (int chatId, int id)[] SentMessages => System.IO.File
            .ReadAllText(_sentMessageFile)
            .Split('\n')
            .Where (row => !string.IsNullOrEmpty(row))
            .Select(row => row.Split(','))
            .Where (col => !string.IsNullOrEmpty(col[0]) && !string.IsNullOrEmpty(col[1]))
            .Select(col => (int.Parse(col[0]), int.Parse(col[1])))
            .ToArray();

        private static void CreateSentMessageFile()
        {
            lock (_sentMessageFile)
            {
                _sentMessageFile = Path.Join(Environment.CurrentDirectory, _sentMessageFile);

                if (!System.IO.File.Exists(_sentMessageFile))
                    System.IO.File.Create(_sentMessageFile);
            }

            Thread.Sleep(1000);
        }
        
        private static void StoreSentMessage(Message message)
        {
            lock (_sentMessageFile)
            {
                System.IO.File.AppendAllTextAsync(_sentMessageFile, $"{_chatId},{message.MessageId}\n");
            }
        }

        public static void RemoveOldInlineKeyboards()
        {
            if (!System.IO.File.Exists(_sentMessageFile)) return;            

            foreach (var message in SentMessages)
                _bot.EditMessageReplyMarkupAsync(message.chatId, message.id);

            lock (_sentMessageFile)
            {
                System.IO.File.WriteAllText(_sentMessageFile, "");
            }
        }

        public static void UpdateInlineKeyboard(Message message, InlineKeyboard options)
        {
            InlineKeyboardMarkup replyMarkup = null;
            if (options != null) replyMarkup = options.Markup;

            _bot.EditMessageReplyMarkupAsync(message.Chat.Id, message.MessageId, replyMarkup);
        }

        private static void OnInlineButtonPressed(CallbackQuery callbackQuery)
        {
            if (!_inlineReplyCallbacks.ContainsKey(callbackQuery.Data)) return;
            _inlineReplyCallbacks[callbackQuery.Data].Invoke(callbackQuery);
        }

        private static void OnReceiveMessage(Message message)
        {
            Console.WriteLine(message.Text);
        }

        public static async void Send
        (
            string         text,
            ParseMode      parseMode = ParseMode.Markdown,
            InlineKeyboard options   = null
        )
        {
            InlineKeyboardMarkup replyMarkup = null;
            if (options != null) replyMarkup = options.Markup;

            var message = await _bot.SendTextMessageAsync
            (
                chatId:      _chatId,
                text:        text, 
                parseMode:   parseMode,
                replyMarkup: replyMarkup
            );

            StoreSentMessage(message);
        }

        public static async void SendImage
        (
            Image          image,
            string         caption   = null,
            ParseMode      parseMode = ParseMode.Markdown,
            InlineKeyboard options   = null
        )
        {
            using (var imageStream = new MemoryStream())
            {
                image.Save(imageStream, ImageFormat.Png);
                imageStream.Position = 0;

                InlineKeyboardMarkup replyMarkup = null;
                if (options != null) replyMarkup = options.Markup;

                var message = await _bot.SendPhotoAsync
                (
                    chatId:      _chatId,
                    photo:       new InputOnlineFile(imageStream), 
                    caption:     caption, 
                    parseMode:   ParseMode.Markdown,
                    replyMarkup: replyMarkup
                );

                StoreSentMessage(message);
            }
        }
    }
}