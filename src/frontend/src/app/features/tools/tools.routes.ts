import { Routes } from '@angular/router';
import { homeGuard } from '../../core/home/home.guard';

export const toolsRoutes: Routes = [
  {
    path: 'tools',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/tools-hub-page/tools-hub-page').then((m) => m.ToolsHubPage),
  },
  {
    path: 'tools/video-transcription',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/video-transcription-page/video-transcription-page').then(
        (m) => m.VideoTranscriptionPage
      ),
  },
  {
    path: 'tools/video-transcription/:id',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/video-transcription-page/video-transcription-page').then(
        (m) => m.VideoTranscriptionPage
      ),
  },
];
