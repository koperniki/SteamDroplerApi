import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AccountPassportComponent } from './account-passport.component';

describe('AccountPassportComponent', () => {
  let component: AccountPassportComponent;
  let fixture: ComponentFixture<AccountPassportComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AccountPassportComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(AccountPassportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
