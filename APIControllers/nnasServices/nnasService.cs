// controllerbase for all the nnasservices controllers
using Microsoft.AspNetCore.Mvc;
using Account.Filters; 
[ApiController]
[Route(BaseURL)]
[RequireHostConfig("NnasServiceHost")]
[AuthenticateMnid]
public class nnasService : ControllerBase
{
    public const string BaseURL = "/v1/api/"; 
}