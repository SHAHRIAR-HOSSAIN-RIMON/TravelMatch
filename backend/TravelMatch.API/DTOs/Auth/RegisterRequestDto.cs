using System.ComponentModel.DataAnnotations;

using TravelMatch.API.Models;

namespace TravelMatch.API.DTOs.Auth;

public class RegisterRequestDto{
    [Required]
    [MaxLength(100)]
    public string FullName{get;set;} = string.Empty;


    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email{get;set;} =string.Empty;


     [Required]
    [MaxLength(20)]
    public string PhoneNumber{get;set;} = string.Empty;




    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;


    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;



     [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; }






}