using S2S.Shared.CommonResult;
using S2S.Shared.DataTransferObjects.V1.IdentityDTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.ServicesAbstraction
{
	public interface IAuthenticationService
	{
		Task<Result<UserDTO>> LoginAsync(LoginDTO loginDTO);
		Task<Result<UserDTO>> RegisterAsync(RegisterDTO registerDTO);
		Task<bool> CheckEmailAsync(string email);
		Task<Result<UserDTO>> GetUserByEmailAsync(string email);
        

    }
}
