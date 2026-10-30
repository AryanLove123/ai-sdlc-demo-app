import { expect, test } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { InvoicesPage } from '../pages/InvoicesPage';
import { ApprovalsPage } from '../pages/ApprovalsPage';

test('manager over-limit approval is denied and invoice remains pending', async ({ page, browser, baseURL }) => {
  const customerName = `Acme Over Limit ${Date.now()}`;

  const loginPage = new LoginPage(page);
  await loginPage.goto();
  await loginPage.signIn('demo.manager', 'ChangeMe123!');

  const invoicesPage = new InvoicesPage(page);
  await invoicesPage.createInvoice(customerName, 75000);
  await invoicesPage.submitInvoice(customerName);

  const approvalsPage = new ApprovalsPage(page);
  await approvalsPage.open();
  await expect(approvalsPage.pendingItem(customerName)).toBeVisible();
  await approvalsPage.approveInvoice(customerName);

  await expect(approvalsPage.denialAlert('A Manager may approve invoices up to 50,000.')).toBeVisible();

  const freshContext = await browser.newContext({ baseURL });
  try {
    const freshPage = await freshContext.newPage();
    const freshLogin = new LoginPage(freshPage);
    await freshLogin.goto();
    await freshLogin.signIn('demo.director', 'ChangeMe123!');

    const freshInvoices = new InvoicesPage(freshPage);
    await expect(freshInvoices.statusCellForCustomer(customerName)).toBeVisible();
  } finally {
    await freshContext.close();
  }
});