import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AccountTableComponent } from './account/components/account-table/account-table.component';
import { AccountPassportComponent } from './account/components/account-passport/account-passport.component';

const routes: Routes = [
  {path:'accounts', component: AccountTableComponent},
  {path:'account/:accountId', component: AccountPassportComponent},
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
