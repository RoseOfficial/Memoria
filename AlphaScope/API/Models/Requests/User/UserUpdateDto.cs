using AlphaScope.API.Models.Responses.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlphaScope.API.Models.Requests.User
{
    public class UserUpdateDto
    {
        public required List<AlphaScope.API.Models.Responses.User.User.UserCharacterDto?> Characters { get; set; }
    }
}
