import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { DxButtonModule, DxDataGridModule } from 'devextreme-angular';
import { AccountTableComponent } from './account/components/account-table/account-table.component';
import { AccountPassportComponent } from './account/components/account-passport/account-passport.component';
import { AccountService } from './account/services/account.service';
import { HttpClientModule } from '@angular/common/http';

@NgModule({
  declarations: [
    AppComponent, 
    AccountTableComponent, 
    AccountPassportComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    AppRoutingModule, 
    DxButtonModule, 
    DxDataGridModule
  ],
  providers: [AccountService],
  bootstrap: [AppComponent],
})
export class AppModule {}
