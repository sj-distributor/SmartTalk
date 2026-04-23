using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class KnowledgeScenarioController : ControllerBase
{
    private readonly IMediator _mediator;

    public KnowledgeScenarioController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region folder
    
    [Route("folder/list"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetKnowledgeSceneFoldersResponse))]
    public async Task<IActionResult> GetKnowledgeSceneFoldersAsync([FromQuery] GetKnowledgeSceneFoldersRequest request)
    {
        var response = await _mediator.RequestAsync<GetKnowledgeSceneFoldersRequest, GetKnowledgeSceneFoldersResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("folder/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddKnowledgeSceneFolderResponse))]
    public async Task<IActionResult> AddKnowledgeSceneFolderAsync([FromBody] AddKnowledgeSceneFolderCommand command)
    {
        var response = await _mediator.SendAsync<AddKnowledgeSceneFolderCommand, AddKnowledgeSceneFolderResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("folder/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateKnowledgeSceneFolderResponse))]
    public async Task<IActionResult> UpdateKnowledgeSceneFolderAsync([FromBody] UpdateKnowledgeSceneFolderCommand command)
    {
        var response = await _mediator.SendAsync<UpdateKnowledgeSceneFolderCommand, UpdateKnowledgeSceneFolderResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("folder/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteKnowledgeSceneFolderResponse))]
    public async Task<IActionResult> DeleteKnowledgeSceneFolderAsync([FromBody] DeleteKnowledgeSceneFolderCommand command)
    {
        var response = await _mediator.SendAsync<DeleteKnowledgeSceneFolderCommand, DeleteKnowledgeSceneFolderResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    #endregion
    
    #region scene
    [Route("scene/list"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetKnowledgeScenesResponse))]
    public async Task<IActionResult> GetKnowledgeScenesAsync([FromQuery] GetKnowledgeScenesRequest request)
    {
        var response = await _mediator.RequestAsync<GetKnowledgeScenesRequest, GetKnowledgeScenesResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("scene/get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetKnowledgeSceneResponse))]
    public async Task<IActionResult> GetKnowledgeSceneAsync([FromQuery] GetKnowledgeSceneRequest request)
    {
        var response = await _mediator.RequestAsync<GetKnowledgeSceneRequest, GetKnowledgeSceneResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("scene/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddKnowledgeSceneResponse))]
    public async Task<IActionResult> AddKnowledgeSceneAsync([FromBody] AddKnowledgeSceneCommand command)
    {
        var response = await _mediator.SendAsync<AddKnowledgeSceneCommand, AddKnowledgeSceneResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("scene/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateKnowledgeSceneResponse))]
    public async Task<IActionResult> UpdateKnowledgeSceneAsync([FromBody] UpdateKnowledgeSceneCommand command)
    {
        var response = await _mediator.SendAsync<UpdateKnowledgeSceneCommand, UpdateKnowledgeSceneResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    #endregion

    #region scene_knowledge
    [Route("scene/knowledge/get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetKnowledgeSceneKnowledgesResponse))]
    public async Task<IActionResult> GetKnowledgeSceneKnowledgesAsync([FromQuery] GetKnowledgeSceneKnowledgesRequest request)
    {
        var response = await _mediator.RequestAsync<GetKnowledgeSceneKnowledgesRequest, GetKnowledgeSceneKnowledgesResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("scene/knowledge/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddKnowledgeSceneKnowledgeResponse))]
    public async Task<IActionResult> AddKnowledgeSceneKnowledgeAsync([FromBody] AddKnowledgeSceneKnowledgeCommand command)
    {
        var response = await _mediator.SendAsync<AddKnowledgeSceneKnowledgeCommand, AddKnowledgeSceneKnowledgeResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("scene/knowledge/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateKnowledgeSceneKnowledgeResponse))]
    public async Task<IActionResult> UpdateKnowledgeSceneKnowledgeAsync([FromBody] UpdateKnowledgeSceneKnowledgeCommand command)
    {
        var response = await _mediator.SendAsync<UpdateKnowledgeSceneKnowledgeCommand, UpdateKnowledgeSceneKnowledgeResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("scene/knowledge/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteKnowledgeSceneKnowledgeResponse))]
    public async Task<IActionResult> DeleteKnowledgeSceneKnowledgeAsync([FromBody] DeleteKnowledgeSceneKnowledgeCommand command)
    {
        var response = await _mediator.SendAsync<DeleteKnowledgeSceneKnowledgeCommand, DeleteKnowledgeSceneKnowledgeResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    #endregion
}
