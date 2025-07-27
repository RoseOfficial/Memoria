using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AlphaScope.API.Models.User;

namespace AlphaScope.API.Models
{
    public class UserUpdateDto
    {
        public required List<UserCharacterDto?> Characters { get; set; }
    }
}
