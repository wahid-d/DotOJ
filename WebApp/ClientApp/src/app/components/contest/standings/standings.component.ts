﻿import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { saveAs } from 'file-saver';
import * as moment from 'moment';
import * as excel from 'exceljs';

import { ContestService } from '../../../services/contest.service';
import { RegistrationInfoDto } from '../../../interfaces/registration.interfaces';
import { ContestViewDto } from '../../../interfaces/contest.interfaces';
import { ProblemInfoDto } from '../../../interfaces/problem.interfaces';
import { Title } from '@angular/platform-browser';
import { Column } from 'exceljs';

@Component({
  selector: 'app-contest-standings',
  templateUrl: './standings.component.html',
  styleUrls: ['./standings.component.css']
})
export class ContestStandingsComponent implements OnInit {
  public loading = true;
  public contestId: number;
  public contest: ContestViewDto;
  public registrations: RegistrationInfoDto[];

  constructor(
    private title: Title,
    private route: ActivatedRoute,
    private service: ContestService
  ) {
    this.contestId = this.route.snapshot.parent.params.contestId;
  }

  ngOnInit() {
    this.service.getSingle(this.contestId)
      .subscribe(contest => {
        this.contest = contest;
        this.title.setTitle(contest.title + ' - Standings');
        this.loadRegistrations();
      });
  }

  public loadRegistrations() {
    this.loading = true;
    this.service.getRegistrations(this.contestId)
      .subscribe(registrations => {
        for (let i = 0; i < registrations.length; ++i) {
          const registration = registrations[i];
          registration.solved = this.getTotalSolved(registration);
          registration.score = this.getTotalScore(registration);
          registration.penalties = this.getTotalPenalties(registration);
        }
        registrations = registrations.sort((a, b) => {
          if (a.isParticipant != b.isParticipant) {
            return a.isParticipant ? -1 : 1;
          } else if (a.score !== b.score) {
            return b.score - a.score; // descending in score order
          } else {
            return a.penalties - b.penalties; // ascending in penalty order
          }
        });
        for (let i = 0, rank = 0, delta = 1; i < registrations.length; ++i) {
          if (i === 0 || registrations[i].score !== registrations[i - 1].score ||
            registrations[i].penalties !== registrations[i - 1].penalties) {
            rank += delta;
            delta = 1;
          } else {
            ++delta;
          }
          registrations[i].rank = registrations[i].isParticipant ? rank : -1;
        }
        this.registrations = registrations;
        this.loading = false;
      });
  }

  public getProblemPenalty(registration: RegistrationInfoDto, problem: ProblemInfoDto): number {
    const statistic = registration.statistics.find(s => s.problemId === problem.id);
    if (statistic && statistic.acceptedAt) {
      const minutes = (statistic.acceptedAt as moment.Moment).diff(this.contest.beginTime, 'minutes');
      return minutes + 20 * statistic.penalties;
    } else {
      return 0;
    }
  }

  public getProblemItem(registration: RegistrationInfoDto, problem: ProblemInfoDto): string[] | null {
    const statistic = registration.statistics.find(s => s.problemId === problem.id);
    if (statistic) {
      if (statistic.acceptedAt) {
        const minutes = (statistic.acceptedAt as moment.Moment).diff(this.contest.beginTime, 'minutes');
        const penalties = statistic.penalties + 1;
        return [minutes.toString(), penalties.toString() + ' ' + (penalties === 1 ? 'try' : 'tries')];
      } else {
        return ['-', (statistic.penalties).toString() + ' ' + (statistic.penalties === 1 ? 'try' : 'tries')];
      }
    } else {
      return null;
    }
  }

  public getTotalSolved(registration: RegistrationInfoDto): number {
    return registration.statistics.filter(s => s.acceptedAt != null).length;
  }

  public getTotalScore(registration: RegistrationInfoDto): number {
    return registration.statistics.reduce((total, statistic) => total + statistic.score, 0);
  }

  public getTotalPenalties(registration: RegistrationInfoDto): number {
    return this.contest.problems.reduce((total, problem) => total + this.getProblemPenalty(registration, problem), 0);
  }

  public exportStandings() {
    const workbook = new excel.Workbook();
    const sheet = workbook.addWorksheet(this.contest.title);
    sheet.columns = ([
      { header: 'Rank', key: 'rank' },
      { header: 'Contestant ID', key: 'id' },
      { header: 'Contestant Name', key: 'name' },
    ] as Array<Partial<Column>>).concat(this.contest.problems.map(p => {
      return { header: p.label + ' - ' + p.title, key: p.label };
    })).concat([
      { header: 'Solved', key: 'solved' },
      { header: 'Score', key: 'score' },
      { header: 'Penalties', key: 'penalties' }
    ]);
    for (const registration of this.registrations) {
      const row = {
        rank: registration.rank,
        id: registration.contestantId,
        name: registration.contestantName + (registration.isParticipant ? '' : '*'),
        solved: registration.solved,
        score: registration.score,
        penalties: registration.penalties
      };
      for (const problem of this.contest.problems) {
        const item = this.getProblemItem(registration, problem);
        row[problem.label] = item ? (item[0] + ' (' + item[1] + ')') : '';
      }
      sheet.addRow(row);
    }
    workbook.xlsx.writeBuffer().then(data => {
      saveAs(new Blob([data]), this.contest.title + "-standings.xlsx");
    });
  }
}
