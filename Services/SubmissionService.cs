﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Judge1.Data;
using Judge1.Exceptions;
using Judge1.Models;
using Judge1.Runners;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Judge1.Services
{
    public interface ISubmissionService
    {
        public Task<PaginatedList<SubmissionInfoDto>> GetPaginatedSubmissionsAsync
            (int? contestId, int? problemId, string userId, Verdict? verdict, int? pageIndex);

        public Task<SubmissionInfoDto> GetSubmissionInfoAsync(int id, string userId);
        public Task<SubmissionViewDto> GetSubmissionViewAsync(int id, string userId);
        public Task<SubmissionInfoDto> CreateSubmissionAsync(SubmissionCreateDto dto, string userId);
    }

    public class SubmissionService : ISubmissionService
    {
        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<SubmissionService> _logger;
        private readonly ISubmissionRunner _runner;

        public SubmissionService(ApplicationDbContext context,
            ILogger<SubmissionService> logger, ISubmissionRunner runner)
        {
            _context = context;
            _logger = logger;
            _runner = runner;
        }

        private async Task<bool> CanViewSubmission(Submission submission, string userId)
        {
            return submission.UserId == userId
                   || await _context.Submissions.AnyAsync(s => s.Id == submission.Id
                                                               && s.UserId == userId
                                                               && s.Verdict == Verdict.Accepted);
        }

        private async Task ValidateSubmissionCreateDto(SubmissionCreateDto dto, string userId)
        {
            var problem = await _context.Problems.FindAsync(dto.ProblemId);
            if (problem is null)
            {
                throw new ValidationException("Invalid problem ID.");
            }

            var contest = await _context.Contests.FindAsync(problem.ContestId);
            if (contest.IsPublic)
            {
                if (DateTime.Now.ToUniversalTime() < contest.BeginTime)
                {
                    throw new UnauthorizedAccessException("Cannot submit until contest has begun.");
                }
            }
            else
            {
                var registered = await _context.Registrations
                    .AnyAsync(r => r.ContestId == contest.Id && r.UserId == userId);
                if (DateTime.Now.ToUniversalTime() < contest.BeginTime ||
                    (!registered && DateTime.Now.ToUniversalTime() < contest.EndTime))
                {
                    throw new UnauthorizedAccessException("Cannot submit until contest has begun.");
                }
            }
        }

        public async Task<PaginatedList<SubmissionInfoDto>> GetPaginatedSubmissionsAsync
            (int? contestId, int? problemId, string userId, Verdict? verdict, int? pageIndex)
        {
            var submissions = _context.Submissions.AsQueryable();

            if (contestId.HasValue)
            {
                var problemIds = await _context.Problems
                    .Where(p => p.ContestId == contestId.GetValueOrDefault())
                    .Select(p => p.Id)
                    .ToListAsync();
                submissions = submissions.Where(s => problemIds.Contains(s.ProblemId));
            }

            if (problemId.HasValue)
            {
                submissions = submissions.Where(s => s.ProblemId == problemId.GetValueOrDefault());
            }

            if (!string.IsNullOrEmpty(userId))
            {
                submissions = submissions.Where(s => s.UserId == userId);
            }

            if (verdict.HasValue)
            {
                submissions = submissions.Where(s => s.Verdict == verdict.GetValueOrDefault());
            }

            return await submissions.OrderByDescending(s => s.Id)
                .PaginateAsync(s => new SubmissionInfoDto(s), pageIndex ?? 1, PageSize);
        }

        public async Task<SubmissionInfoDto> GetSubmissionInfoAsync(int id, string userId)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission == null)
            {
                throw new NotFoundException();
            }

            return new SubmissionInfoDto(submission);
        }

        public async Task<SubmissionViewDto> GetSubmissionViewAsync(int id, string userId)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission == null)
            {
                throw new NotFoundException();
            }

            if (!await CanViewSubmission(submission, userId))
            {
                throw new UnauthorizedAccessException("Not allowed to view this submission.");
            }

            return new SubmissionViewDto(submission);
        }

        public async Task<SubmissionInfoDto> CreateSubmissionAsync(SubmissionCreateDto dto, string userId)
        {
            await ValidateSubmissionCreateDto(dto, userId);
            var submission = new Submission()
            {
                UserId = userId,
                ProblemId = dto.ProblemId.GetValueOrDefault(),
                Program = dto.Program
            };
            await _context.Submissions.AddAsync(submission);
            await _context.SaveChangesAsync();

            _runner.RunInBackground(submission.Id);

            return new SubmissionInfoDto(submission);
        }
    }
}