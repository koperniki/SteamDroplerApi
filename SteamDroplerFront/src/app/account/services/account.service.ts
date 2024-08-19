import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Account } from '../models/account';

@Injectable({
  providedIn: 'root'
})
export class AccountService {

  constructor(private readonly http: HttpClient) { }

  public getAccounts(): Observable<Account[]> {
    return this.http.get<Account[]>("http://localhost:7832/api/account")
  }

  public getAccount(accountId: string): Observable<Account> {
    return this.http.get<Account>(`http://localhost:7832/api/account/${accountId}`);
  }
}
