using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AlphaScope.API.Models.Requests.User
{
    public class UserRegister
    {
        public int GameAccountId { get; set; }
        public long UserLocalContentId { get; set; }
        public required string Name { get; set; }
        public required string ClientId { get; set; }
        public required string Version { get; set; }
    }
}
