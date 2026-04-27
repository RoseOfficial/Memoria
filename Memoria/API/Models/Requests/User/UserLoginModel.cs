using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoria.API.Models.Requests.User
{
    public class UserLoginModel
    {
        public long GameAccountId { get; set; }
        public string Password { get; set; } = null!;
    }
}
