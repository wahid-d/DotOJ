﻿import { Component, Input, OnChanges, OnDestroy, OnInit, SimpleChanges } from '@angular/core';
import { interval, Observable, Subject } from 'rxjs';
import { map, take, takeUntil } from 'rxjs/operators';

import { notAnValidAttempt, VerdictInfo, VerdictStage } from '../../../consts/verdicts.consts';
import { SubmissionService } from '../../../services/submission.service';
import { PaginatedList } from '../../../interfaces/pagination.interfaces';
import { SubmissionInfoDto } from '../../../interfaces/submission.interfaces';
import { AuthorizeService } from '../../../../api-authorization/authorize.service';

@Component({
  selector: 'app-submission-timeline',
  templateUrl: './timeline.component.html',
  styleUrls: ['./timeline.component.css']
})
export class SubmissionTimelineComponent implements OnInit, OnChanges, OnDestroy {
  notAnValidAttempt = notAnValidAttempt;

  @Input() public problemId: number;

  private destroy$ = new Subject();
  public userId: Observable<string>;
  public list: PaginatedList<SubmissionInfoDto>;
  public submissions: SubmissionInfoDto[];

  constructor(
    private auth: AuthorizeService,
    private service: SubmissionService
  ) {
    this.userId = this.auth.getUser().pipe(map(u => u && u.sub));
  }

  ngOnInit() {
    interval(2000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (this.submissions.filter(s => {
          const verdict = s.verdict as VerdictInfo;
          return verdict.stage === VerdictStage.RUNNING || (verdict.stage === VerdictStage.REJECTED && s.score == null);
        }).length > 0) {
          this.updatePendingSubmissions();
        }
      });
    this.service.newSubmission
      .pipe(takeUntil(this.destroy$))
      .subscribe(submission => this.submissions.unshift(submission));
  }

  ngOnChanges(changes: SimpleChanges) {
    this.loadSubmissions(changes.problemId.currentValue);
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadSubmissions(problemId: number): void {
    this.submissions = [];
    this.userId.pipe(take(1)).subscribe(userId => {
      this.service.getPaginatedList(null, this.problemId, userId, null, 1)
        .subscribe(list => {
          for (let i = 0; i < list.items.length; ++i) {
            this.submissions.unshift(list.items[i]);
          }
        });
    });
  }

  private updatePendingSubmissions(): void {
    const submissionIds = this.submissions.filter(s => {
      const verdict = s.verdict as VerdictInfo;
      return verdict.stage === VerdictStage.RUNNING || (verdict.stage === VerdictStage.REJECTED && s.score == null);
    }).map(s => s.id);
    this.service.getBatchInfos(submissionIds)
      .subscribe(updatedSubmissions => {
        for (let i = 0; i < updatedSubmissions.length; ++i) {
          const updated = updatedSubmissions[i];
          const submission = this.submissions.find(s => s.id === updated.id);
          submission.verdict = updated.verdict;
          submission.time = updated.time;
          submission.memory = updated.memory;
          submission.failedOn = updated.failedOn;
          submission.score = updated.score;
          submission.progress = updated.progress;
          submission.judgedAt = updated.judgedAt;
        }
      });
  }

  public getSubmissionPct(submission: SubmissionInfoDto): string {
    const verdict = submission.verdict as VerdictInfo;
    if (verdict.stage === VerdictStage.RUNNING && submission.progress) {
      return submission.progress + '%';
    } else if (verdict.stage === VerdictStage.REJECTED && submission.score == null && submission.progress) {
      return '(Running ' + submission.progress + '%)';
    } else if (submission.failedOn > 0 && submission.score >= 0 && verdict.showCase) {
      if (verdict.stage === VerdictStage.REJECTED) {
        return (100 - submission.score) + '%';
      } else {
        return submission.score + '%';
      }
    }
    return null;
  }
}
