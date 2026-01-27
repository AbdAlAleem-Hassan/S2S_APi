using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.IdentityDTOs
{
    public record UpdateProfileDTO
        (
            string DisplayName ,
            string? PhoneNumber = null,
            IFormFile? ProfileImage = null
        );
}
