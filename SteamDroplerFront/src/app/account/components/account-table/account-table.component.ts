import { Component } from '@angular/core';
import { Observable } from 'rxjs';
import { Account } from '../../models/account';
import { AccountService } from '../../services/account.service';

@Component({
  selector: 'app-account-table',
  templateUrl: './account-table.component.html',
  styleUrl: './account-table.component.css'
})
export class AccountTableComponent {

  
  public dataSource: Observable<Account[]> | null = null;

  constructor(private readonly accountService: AccountService) {
    this.reload();
  }

  private reload(): void {
    this.dataSource = this.accountService.getAccounts();
  }

}
