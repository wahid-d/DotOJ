﻿using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Data.Models;
using Data.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notification;
using Worker.Models;
using Worker.Runners.JudgeSubmission.ContestModes;

namespace Worker.Runners.JudgeSubmission
{
    public sealed class SubmissionRunner : JobRunnerBase<SubmissionRunner>
    {
        public SubmissionRunner(IServiceProvider provider) : base(provider)
        {
        }

        public override async Task<int> HandleJobRequest(JobRequestMessage message)
        {
            var submission = await Context.Submissions.FindAsync(message.TargetId);
            if (submission is null || submission.RequestVersion >= message.RequestVersion)
            {
                Logger.LogDebug($"IgnoreJudgeRequestMessage" +
                                $" SubmissionId={message.TargetId}" +
                                $" RequestVersion={message.RequestVersion}");
                return 0;
            }
            else
            {
                submission.Verdict = Verdict.Running;
                submission.Time = null;
                submission.Memory = null;
                submission.FailedOn = null;
                submission.Score = null;
                submission.Progress = 0;
                submission.JudgedBy = Options.Value.Name;
                submission.RequestVersion = message.RequestVersion;
                await Context.SaveChangesAsync();
            }

            var user = await Context.Users.FindAsync(submission.UserId);
            var problem = await Context.Problems.FindAsync(submission.ProblemId);
            var contest = await Context.Contests.FindAsync(problem.ContestId);

            try
            {
                Logger.LogInformation($"RunSubmission Id={submission.Id} Problem={problem.Id}");
                var stopwatch = Stopwatch.StartNew();

                var result = await this.RunSubmissionAsync(contest, problem, submission);

                #region Update judge result of submission

                submission.Verdict = result.Verdict;
                submission.Time = result.Time;
                submission.Memory = result.Memory;
                submission.FailedOn = result.FailedOn;
                submission.Score = result.Score;
                submission.Progress = 100;
                submission.Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(result.Message));
                submission.JudgedAt = DateTime.Now.ToUniversalTime();

                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    // Validate that the submission is not touched by others since picking up.
                    var fetched = await Context.Submissions.FindAsync(submission.Id);
                    if (fetched.RequestVersion == message.RequestVersion && fetched.JudgedBy == Options.Value.Name)
                    {
                        Context.Submissions.Update(submission);
                        await Context.SaveChangesAsync();
                    }
                    scope.Complete();
                }

                #endregion

                #region Rebuild statistics of registration

                // TODO: remove this part to webapp
                if (submission.CreatedAt >= contest.BeginTime && submission.CreatedAt <= contest.EndTime)
                {
                    var registration = await Context.Registrations.FindAsync(user.Id, contest.Id);
                    if (registration != null)
                    {
                        await registration.RebuildStatisticsAsync(Context);
                        Context.Registrations.Update(registration);
                        await Context.SaveChangesAsync();
                    }
                }

                #endregion

                stopwatch.Stop();
                Logger.LogInformation($"RunSubmission Complete Submission={submission.Id} Problem={problem.Id}" +
                                      $" Verdict={submission.Verdict} TimeElapsed={stopwatch.Elapsed}");
            }
            catch (Exception e)
            {
                var error = $"Internal error: {e.Message}\n" +
                            $"Occurred at {DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC @ {Options.Value.Name}\n" +
                            $"*** Please report this incident to TA and site administrator ***";
                submission.Verdict = Verdict.Failed;
                submission.Time = submission.Memory = null;
                submission.FailedOn = null;
                submission.Score = 0;
                submission.Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(error));
                submission.JudgedAt = DateTime.Now.ToUniversalTime();
                submission.JudgedBy = Options.Value.Name;

                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    // Validate that the submission is not touched by others since picking up.
                    var fetched = await Context.Submissions.FindAsync(submission.Id);
                    if (fetched.RequestVersion == message.RequestVersion && fetched.JudgedBy == Options.Value.Name)
                    {
                        Context.Submissions.Update(submission);
                        await Context.SaveChangesAsync();
                    }
                    scope.Complete();
                }

                Logger.LogError($"RunSubmission Error Submission={submission.Id} Error={e.Message}\n" +
                                $"Stacktrace of error:\n{e.StackTrace}");
                var broadcaster = Provider.GetRequiredService<INotificationBroadcaster>();
                await broadcaster.SendNotification(true, $"Runner failed on Submission #{submission.Id}",
                    $"Submission runner \"{Options.Value.Name}\" failed on submission #{submission.Id}" +
                    $" with error message **\"{e.Message}\"**.");
            }

            return submission.RequestVersion;
        }

        private async Task<JudgeResult> RunSubmissionAsync(Contest contest, Problem problem, Submission submission)
        {
            ContestRunnerBase runner;
            switch (contest.Mode)
            {
                case ContestMode.Practice:
                    runner = new PracticeRunner(contest, problem, submission, Provider);
                    break;
                case ContestMode.UntilFail:
                    runner = new UntilFailRunner(contest, problem, submission, Provider);
                    break;
                case ContestMode.OneShot:
                    runner = new OneShotRunner(contest, problem, submission, Provider);
                    break;
                case ContestMode.SampleOnly:
                    runner = new SampleOnlyRunner(contest, problem, submission, Provider);
                    break;
                default:
                    throw new Exception($"Unknown contest mode ${contest.Mode}");
            }

            return await runner.RunSubmissionAsync();
        }
    }
}