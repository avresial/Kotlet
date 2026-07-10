import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminUser } from '../../admin.models';
import { AdminPage } from './admin-page';

describe('AdminPage pagination', () => {
  function makeUser(index: number): AdminUser {
    return {
      id: `user-${index}`,
      email: `user-${index}@example.com`,
      displayName: `User ${index}`,
      preferredLanguage: null,
      createdAtUtc: '2026-06-29T00:00:00Z',
      updatedAtUtc: '2026-06-29T00:00:00Z',
      lastLoginAtUtc: null,
      roles: ['User'],
    };
  }

  function renderWithTotalCount(totalCount: number) {
    TestBed.configureTestingModule({
      imports: [AdminPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(AdminPage);
    const pageSize = fixture.componentInstance.pageSize;
    const items = Array.from({ length: Math.min(totalCount, pageSize) }, (_, i) => makeUser(i + 1));

    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/admin/users').flush({ items, page: 1, pageSize, totalCount });
    fixture.detectChanges();
    http.verify();
    return fixture;
  }

  it('hides the pagination controls when all users fit on a single page', () => {
    const fixture = renderWithTotalCount(5);
    expect(fixture.nativeElement.querySelector('.pagination')).toBeNull();
  });

  it('hides the pagination controls when the results exactly fill one page', () => {
    const fixture = renderWithTotalCount(10);
    expect(fixture.nativeElement.querySelector('.pagination')).toBeNull();
  });

  it('shows the pagination controls when users span multiple pages', () => {
    const fixture = renderWithTotalCount(11);
    expect(fixture.nativeElement.querySelector('.pagination')).not.toBeNull();
  });
});
