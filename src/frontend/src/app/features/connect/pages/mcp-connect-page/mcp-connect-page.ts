import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { McpDiscoveryDocument, McpService } from '../../mcp.service';

@Component({
  selector: 'app-mcp-connect-page',
  imports: [ButtonModule, MessageModule, RouterLink, TranslatePipe],
  templateUrl: './mcp-connect-page.html',
  styleUrl: './mcp-connect-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class McpConnectPage implements OnInit {
  private readonly mcp = inject(McpService);

  readonly discovery = signal<McpDiscoveryDocument | null>(null);
  readonly copied = signal<string | null>(null);

  readonly serverUrl = computed(() => this.discovery()?.mcp_endpoint ?? '');
  readonly clientId = computed(() => this.discovery()?.client_id ?? '');
  readonly authorizationEndpoint = computed(() => this.discovery()?.authorization_endpoint ?? '');
  readonly tokenEndpoint = computed(() => this.discovery()?.token_endpoint ?? '');
  readonly manifestJson = computed(() => {
    const document = this.discovery();
    return document ? JSON.stringify(this.mcp.toManifest(document), null, 2) : '';
  });

  ngOnInit(): void {
    this.mcp.discover().subscribe((document) => this.discovery.set(document));
  }

  copy(value: string, marker: string): void {
    if (!value) return;
    void navigator.clipboard?.writeText(value).then(() => {
      this.copied.set(marker);
      setTimeout(() => this.copied.update((current) => (current === marker ? null : current)), 2000);
    });
  }

  downloadManifest(): void {
    const json = this.manifestJson();
    if (!json) return;
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'kotlet-mcp.json';
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
