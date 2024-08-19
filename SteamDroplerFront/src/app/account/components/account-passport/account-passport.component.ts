import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { Account } from '../../models/account';

@Component({
  selector: 'app-account-passport',
  templateUrl: './account-passport.component.html',
  styleUrl: './account-passport.component.css'
})
export class AccountPassportComponent implements OnInit{

  public accountId: string | null = null;
  public account: Account | null = null;

  constructor(private readonly route: ActivatedRoute, private readonly accountService: AccountService) {
  }

  public async ngOnInit(): Promise<void> {
    this.accountId = this.route.snapshot.paramMap.get('accountId');
    await this.realod();
  }

  private async realod(): Promise<void> {
    this.account = await firstValueFrom(this.accountService.getAccount(this.accountId!));
  }

}
