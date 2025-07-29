using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlphaScope.API.Models.User
{
    public class UserUpdateDto
    {
        public required List<User.UserCharacterDto?> Characters { get; set; }
    }
}
