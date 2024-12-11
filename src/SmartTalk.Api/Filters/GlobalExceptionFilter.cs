using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartTalk.Messages.Responses;
using SmartTalk.Core.Middlewares.Security;

namespace SmartTalk.Api.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            AccountExpiredException => HttpStatusCode.Unauthorized,
            _ => HttpStatusCode.InternalServerError
        };

        context.Result = new OkObjectResult(new SmartTalkResponse()
        {
            Code = statusCode,
            Msg = context.Exception.Message
        });

        context.ExceptionHandled = true;
    }
}