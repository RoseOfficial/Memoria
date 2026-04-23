using Microsoft.AspNetCore.Mvc;
using AlphaScopeServer.Data;

namespace AlphaScopeServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AlphaScopeDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AlphaScopeDbContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }
    }
}
