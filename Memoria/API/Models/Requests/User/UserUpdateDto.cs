using Memoria.API.Models.Responses.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoria.API.Models.Requests.User
{
    public class UserUpdateDto
    {
        public required List<Memoria.API.Models.Responses.User.User.UserCharacterDto?> Characters { get; set; }
    }
}
