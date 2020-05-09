using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DiscordBot.Data;
using LiteDB;
using PixivCS;

namespace DiscordBot.Modules
{
    [Group("pixiv")]
    public class PixivModule : ModuleBase<SocketCommandContext>
    {
        public LiteDatabase Database { get; set; }
        public Dictionary<ulong, PixivAppAPI> PixivAppApiCache { get; set; }

        // Send default pixiv image '!pixiv' or '!pixiv default'
        [Command("login")]
        public async Task Login(string username, string password)
        {
            // get pixiv api from cache is possible
            PixivAppApiCache.TryGetValue(Context.User.Id, out var pixivAppApi);
            pixivAppApi ??= new PixivAppAPI();
            PixivAppApiCache[Context.User.Id] = pixivAppApi;

            var auth = await pixivAppApi.Auth(username, password);
            var json = await FormatJson(auth);
            // await Context.Channel.SendMessageAsync(json);
            string refreshToken;
            string name;
            string id;
            try
            {
                refreshToken = auth.RootElement.GetProperty("response").GetProperty("refresh_token").GetString();
                id = auth.RootElement.GetProperty("response").GetProperty("user").GetProperty("id").GetString();
                name = auth.RootElement.GetProperty("response").GetProperty("user").GetProperty("name").GetString();
            }
            catch (Exception e)
            {
                await Context.Channel.SendMessageAsync(
                    $"Failed to login to pixiv with the provided account password:\n{json}");
                return;
            }

            if (string.IsNullOrEmpty(refreshToken))
            {
                await Context.Channel.SendMessageAsync(
                    $"Failed to login to pixiv with the provided account password:\n{json}");
                return;
            }

            var users = Database.GetCollection<User>("users");
            var user = users.FindOne(u => u.Id == Context.User.Id) ?? new User {Id = Context.User.Id};
            user.PixivUsername = username;
            user.PixivPassword = password;
            user.PixivId = id;
            user.PixivRefreshToken = refreshToken;
            users.Upsert(user);
            await Context.Channel.SendMessageAsync(
                $"Login successfully as {name} ({id})");
        }

        private static async Task<string> FormatJson(JsonDocument jsonDocument)
        {
            await using var stream = new MemoryStream();
            Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions {Indented = true});
            jsonDocument.WriteTo(writer);
            await writer.FlushAsync();
            string json = Encoding.UTF8.GetString(stream.ToArray());
            return json;
        }

        // Send default pixiv image '!pixiv' or '!pixiv default'
        [Command, Alias("random")]
        public Task SendDefaultImageAsync()
            => SendIllustRankingImage();

        // Send pixiv image by id '!pixiv [id]'
        [Command]
        public Task SendImageByIdAsync(ulong illustId)
            => SendPixivImage(illustId.ToString());

        private async Task SendPixivImage(string illustId)
        {
            var pixivAppApi = await GetPixivAppApi();

            var illustDetail = await pixivAppApi.IllustDetail(illustId);

            string url;
            try
            {
                // url = illustDetail.RootElement.GetProperty("illust").GetProperty("image_urls").GetProperty("large")
                //     .ToString();
                url = illustDetail.RootElement.GetProperty("illust").GetProperty("meta_single_page").GetProperty("original_image_url").GetString();

            }
            catch (Exception e)
            {
                await Context.Channel.SendMessageAsync(
                    $"Failed to query illust with id {illustId}");
                return;
            }

            await using var resStream = await (await pixivAppApi.RequestCall("GET",
                    url, new Dictionary<string, string>() {{"Referer", "https://app-api.pixiv.net/"}})).Content
                .ReadAsStreamAsync();
            await Context.Channel.SendFileAsync(resStream, "image.jpg");
        }

        private async Task SendIllustRankingImage(string mode = "day")
        {
            var pixivAppApi = await GetPixivAppApi();

            var illustRanking = await pixivAppApi.IllustRanking();

            string url;
            string title;
            ulong id;
            string author;
            try
            {
                // var json = await FormatJson(illustRanking);
                // await Context.Channel.SendMessageAsync(json);
                // return;

                var illusts = illustRanking.RootElement.GetProperty("illusts");
                var illustCount = illusts.GetArrayLength();
                var illustsEnumerator = illusts.EnumerateArray();
                Random rand = new Random();
                int toSkip = rand.Next(0, illustCount);
                var randomIllust = illustsEnumerator.Skip(toSkip).Take(1).FirstOrDefault();
                // url = randomIllust.GetProperty("image_urls").GetProperty("large").GetString();
                url = randomIllust.GetProperty("meta_single_page").GetProperty("original_image_url").GetString();
                title = randomIllust.GetProperty("title").GetString();
                id = randomIllust.GetProperty("id").GetUInt64();
                author = randomIllust.GetProperty("user").GetProperty("name").GetString();
            }
            catch (Exception e)
            {
                await Context.Channel.SendMessageAsync(
                    $"Failed to query illustRanking");
                return;
            }

            var embed = new EmbedBuilder
            {
                Title = title,
                Description = id.ToString(),
                Color = new Color(0x0096fa), // color of the day
            };
            embed.AddField("Author", author);

            await ReplyAsync("", embed: embed.Build());
            await using var resStream = await (await pixivAppApi.RequestCall("GET",
                    url, new Dictionary<string, string>() {{"Referer", "https://app-api.pixiv.net/"}})).Content
                .ReadAsStreamAsync();
            await Context.Channel.SendFileAsync(resStream, "image.jpg");
        }

        private async Task<PixivAppAPI> GetPixivAppApi()
        {
            var cachedPixivApi = PixivAppApiCache.TryGetValue(Context.User.Id, out var pixivAppApi);
            pixivAppApi ??= new PixivAppAPI();
            PixivAppApiCache[Context.User.Id] = pixivAppApi;

            if (!cachedPixivApi)
            {
                await RefreshPixivAuth(pixivAppApi);
            }

            return pixivAppApi;
        }

        private async Task RefreshPixivAuth(PixivAppAPI pixivAppApi)
        {
            var users = Database.GetCollection<User>("users");
            var user = users.FindOne(u => u.Id == Context.User.Id);
            if (string.IsNullOrEmpty(user.PixivRefreshToken))
            {
                await Context.Channel.SendMessageAsync(
                    $"Please login with `chiaki pixiv login [username] [password]` first");
                return;
            }

            JsonDocument auth;
            try
            {
                auth = await pixivAppApi.Auth(user.PixivRefreshToken);
            }
            catch (Exception e)
            {
                auth = await pixivAppApi.Auth(user.PixivUsername, user.PixivPassword);
            }

            string refreshToken;
            string name;
            string id;
            try
            {
                refreshToken = auth.RootElement.GetProperty("response").GetProperty("refresh_token").GetString();
                id = auth.RootElement.GetProperty("response").GetProperty("user").GetProperty("id").GetString();
                name = auth.RootElement.GetProperty("response").GetProperty("user").GetProperty("name").GetString();
            }
            catch (Exception e)
            {
                await Context.Channel.SendMessageAsync(
                    $"Invalid pixiv credential. Please retry login.");
                return;
            }

            user.PixivId = id;
            user.PixivRefreshToken = refreshToken;
            users.Upsert(user);
        }
    }
}