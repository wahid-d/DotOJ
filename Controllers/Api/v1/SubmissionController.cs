﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using Judge1.Exceptions;
using Judge1.Models;
using Judge1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Judge1.Controllers.Api.v1
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SubmissionController : ControllerBase
    {
        private readonly ISubmissionService _service;
        private readonly ILogger<SubmissionController> _logger;

        public SubmissionController(ISubmissionService service, ILogger<SubmissionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedList<SubmissionInfoDto>>> ListSubmissions(int? pageIndex)
        {
            return Ok(await _service.GetPaginatedSubmissionsAsync(pageIndex));
        }

        [HttpGet("problem-user")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<SubmissionInfoDto>>>
            GetSubmissionsByProblemAndUser(int problemId, string userId)
        {
            return Ok(await _service.GetSubmissionsByProblemAndUserAsync(problemId, userId));
        }

        [HttpGet("{id:int}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubmissionInfoDto>> GetSubmission(int id, bool? simple)
        {
            try
            {
                var submission = await _service.GetSubmissionViewAsync(id, User.GetSubjectId());
                if (simple.GetValueOrDefault(false))
                {
                    submission.Program = null;
                }
                return Ok(submission);
            }
            catch (NotFoundException e)
            {
                return NotFound(e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                return Unauthorized(e.Message);
            }
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<SubmissionInfoDto>> CreateSubmission(SubmissionViewDto dto)
        {
            try
            {
                return Ok(await _service.CreateSubmissionAsync(dto, User.GetSubjectId()));
            }
            catch (ValidationException e)
            {
                return BadRequest(e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                return Unauthorized(e.Message);
            }
        }
    }
}