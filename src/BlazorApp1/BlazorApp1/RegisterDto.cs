using System.ComponentModel.DataAnnotations;

namespace BlazorApp1
{
    public class RegisterDto
    {
        [Display(Name ="User name")]
        [Required(ErrorMessage = "{0} is required.")]
        public string UserName { get; set; }

        [Display(Name = "Password")]
        [Required(ErrorMessage = "{0} is required.")]
        [StringLength(8, ErrorMessage = "PasswordLeastCharactersLong", MinimumLength = 6)]
        public string Password { get; set; }

        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "Password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
