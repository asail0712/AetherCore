using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AetherCore.Auth.LineLogin
{
    public class LineProfileResponse
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("pictureUrl")]
        public string? PictureUrl { get; set; }

        [JsonPropertyName("statusMessage")]
        public string? StatusMessage { get; set; }
    }
}
