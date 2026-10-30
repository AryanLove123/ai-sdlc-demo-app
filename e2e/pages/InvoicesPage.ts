import { type Page, type Locator } from '@playwright/test';

export class InvoicesPage {
  readonly page: Page;
  readonly invoicesTab: Locator;
  readonly customerNameInput: Locator;
  readonly amountInput: Locator;
  readonly createInvoiceButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.invoicesTab = page.getByRole('tab', { name: 'Invoices' });
    this.customerNameInput = page.getByLabel('Customer name');
    this.amountInput = page.getByLabel('Amount');
    this.createInvoiceButton = page.getByRole('button', { name: 'Create invoice' });
  }

  async open() {
    await this.invoicesTab.click();
  }

  async createInvoice(customerName: string, amount: number) {
    await this.customerNameInput.fill(customerName);
    await this.amountInput.fill(String(amount));
    await this.createInvoiceButton.click();
  }

  invoiceRow(customerName: string) {
    return this.page.getByRole('row').filter({ hasText: customerName });
  }

  statusCellForCustomer(customerName: string) {
    return this.invoiceRow(customerName).getByRole('cell', { name: 'Pending Approval' });
  }

  submitInvoice(customerName: string) {
    return this.invoiceRow(customerName).getByRole('button', { name: /Submit invoice/i }).click();
  }
}
