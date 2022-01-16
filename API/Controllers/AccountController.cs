using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class AccountController: BaseApiController
    {
        public DataContext Context { get; }
        private readonly ITokenService _tokenService;
        private readonly IMapper _mapper;

        public AccountController(DataContext context, 
                                ITokenService tokenService,
                                IMapper mapper)
        {
            _tokenService = tokenService;
            _mapper = mapper;
            Context = context;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>>Register(RegisterDto registerDto) {

            if(await UserExists(registerDto.Username)) return BadRequest("Username is taken");
            var user = _mapper.Map<AppUser>(registerDto);

            using var hmac = new HMACSHA512();
            
            user.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password));
            user.PasswordSalt = hmac.Key;
            user.UserName = registerDto.Username.ToLower();
            
            Context.Users.Add(user);
            await Context.SaveChangesAsync();
            return new UserDto {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user),
                KnownAs = user.KnownAs
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto) {            
            var user = await Context.Users
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(user => user.UserName == loginDto.Username.ToLower());
            if (user == null) return Unauthorized("Invalid username");
            using var hmac = new HMACSHA512(user.PasswordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password));
            for (int i = 0; i < computedHash.Length; i++) {
                if (computedHash[i] != user.PasswordHash[i] ) {
                    return BadRequest("Invalid password");
                }
            }
            return new UserDto {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user),
                PhotoUrl = user.Photos.FirstOrDefault(x => x.IsMain)?.Url,
                KnownAs = user.KnownAs
            };
        }

        private async Task<bool>UserExists(string username) {
            return await Context.Users.AnyAsync(user => user.UserName == username.ToLower());
        }
    }
}