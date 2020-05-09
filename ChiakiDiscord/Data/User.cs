namespace DiscordBot.Data
{
    public class User
    {
        public ulong Id { get; set; }
        public int Points { get; set; }
        
        public string PixivId { get; set; }
        public string PixivUsername { get; set; }
        public string PixivPassword { get; set; }
        public string PixivRefreshToken { get; set; }
    }
}