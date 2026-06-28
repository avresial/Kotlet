import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { IncomingInvitation } from '../../home.models';
import { HomeService } from '../../home.service';
import { InvitationInbox } from './invitation-inbox';

describe('InvitationInbox', () => {
  let fixture: ComponentFixture<InvitationInbox>;
  const invitation: IncomingInvitation = {
    id: 'invite-1',
    houseId: 'house-1',
    houseName: 'Test home',
    invitedByName: 'Alex',
    invitedAtUtc: '2026-06-28T10:00:00Z',
  };
  const homeService = {
    accept: () => of({}),
    decline: () => of(undefined),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InvitationInbox],
      providers: [
        { provide: HomeService, useValue: homeService },
        { provide: TranslationService, useValue: { translate: (key: string) => key } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(InvitationInbox);
    fixture.componentRef.setInput('invitations', [invitation]);
    fixture.detectChanges();
  });

  it('emits the invitation id after joining', () => {
    let joinedId: string | undefined;
    fixture.componentInstance.joined.subscribe((id) => joinedId = id);

    fixture.componentInstance.join(invitation);

    expect(joinedId).toBe(invitation.id);
    expect(fixture.componentInstance.busyInvitationId()).toBeNull();
  });

  it('emits the invitation id after rejecting', () => {
    let rejectedId: string | undefined;
    fixture.componentInstance.rejected.subscribe((id) => rejectedId = id);

    fixture.componentInstance.reject(invitation);

    expect(rejectedId).toBe(invitation.id);
    expect(fixture.componentInstance.busyInvitationId()).toBeNull();
  });
});
