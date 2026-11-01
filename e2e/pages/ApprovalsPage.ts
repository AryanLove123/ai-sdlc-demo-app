import { type Page, type Locator } from '@playwright/test';

export class ApprovalsPage {
  readonly page: Page;
  readonly approvalsTab: Locator;

  constructor(page: Page) {
    this.page = page;
    this.approvalsTab = page.getByRole('tab', { name: 'Approvals' });
  }

  async open() {
    await this.approvalsTab.click();
  }

  pendingItem(customerName: string) {
    return this.page.getByRole('listitem').filter({ hasText: customerName });
  }

  approveInvoice(customerName: string) {
    return this.pendingItem(customerName).getByRole('button', { name: /Approve invoice/i }).click();
  }

  denialAlert(message: string) {
    return this.page.getByRole('alert').filter({ hasText: message });
  }
}
