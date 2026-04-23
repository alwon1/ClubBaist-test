/**
 * ClubBaist Detailed Demo Recording Script
 *
 * Covers:
 * 1) Membership lifecycle
 *    - Member self-submits an application
 *    - Admin creates an application
 *    - Committee approves an application
 *    - Approved member signs in and views account page
 * 2) Tee times
 *    - Lower-privileged member books a tee time quickly
 *    - Shareholder submits standing tee time request
 *    - Admin approves standing request
 *    - Week view shown for two different weeks
 * 3) Scores
 *    - Record score
 *    - Show calculated handicap on confirmation
 *
 * Usage:
 *   node record-detailed-demo.mjs
 *
 * Optional env vars:
 *   BASE_URL=https://localhost:7021
 *   DEMO_SLOWMO=350
 *   DEMO_WIDTH=1280
 *   DEMO_HEIGHT=720
 */

import { chromium } from 'playwright';
import path from 'path';
import { fileURLToPath } from 'url';
import fs from 'fs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BASE = process.env.BASE_URL || 'https://localhost:7021';
const SLOW_MO = Number.parseInt(process.env.DEMO_SLOWMO || '350', 10);
const WIDTH = Number.parseInt(process.env.DEMO_WIDTH || '1280', 10);
const HEIGHT = Number.parseInt(process.env.DEMO_HEIGHT || '720', 10);

const VIDEOS_DIR = path.resolve(__dirname, '../videos');
fs.mkdirSync(VIDEOS_DIR, { recursive: true });

function formatTimestamp(ms) {
  const totalSeconds = Math.max(0, Math.floor(ms / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  if (hours > 0) {
    return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  }
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

async function main() {
  const chapterStart = Date.now();
  const chapters = [];

  function addChapter(title, description) {
    chapters.push({
      ms: Date.now() - chapterStart,
      title,
      description,
    });
  }

  function writeChapterFile() {
    if (chapters.length === 0) {
      return null;
    }

    // Ensure the first chapter starts at 00:00 for YouTube chapter parsing.
    chapters[0].ms = 0;

    const lines = [
      'YouTube Chapters',
      '',
      ...chapters.map((c) => `${formatTimestamp(c.ms)} ${c.title} - ${c.description}`),
      '',
      'Paste the timestamp lines into your YouTube video description.',
    ];

    const chapterFileName = `chapters-${new Date().toISOString().replace(/[:.]/g, '-')}.txt`;
    const chapterPath = path.join(VIDEOS_DIR, chapterFileName);
    fs.writeFileSync(chapterPath, lines.join('\n') + '\n', 'utf8');
    return chapterPath;
  }

  const browser = await chromium.launch({
    headless: false,
    slowMo: SLOW_MO,
    args: ['--ignore-certificate-errors'],
  });

  const ctx = await browser.newContext({
    recordVideo: { dir: VIDEOS_DIR, size: { width: WIDTH, height: HEIGHT } },
    ignoreHTTPSErrors: true,
    viewport: { width: WIDTH, height: HEIGHT },
  });

  const pg = await ctx.newPage();

  const pause = (ms) => pg.waitForTimeout(ms);

  async function blazorReady(timeout = 20000) {
    await pg.waitForLoadState('domcontentloaded');
    await pg.waitForFunction(
      () => !document.body.innerText.includes('Loading...'),
      { timeout }
    );
  }

  async function waitForAlertText(regex, timeout = 15000) {
    const alert = pg.locator('[role="alert"], .alert-success, .alert-danger').filter({ hasText: regex }).first();
    await alert.waitFor({ timeout });
    return alert;
  }

  async function login(email, password) {
    await pg.goto(BASE + '/Account/Login');
    await pg.waitForURL('**/Account/Login');
    await pg.getByRole('textbox', { name: 'Email' }).fill(email);
    await pg.getByRole('textbox', { name: 'Password' }).fill(password);
    await pg.getByRole('button', { name: 'Log in', exact: true }).click();
    await pg.waitForURL(BASE + '/');
    await pause(700);
  }

  async function logout() {
    await pg.getByRole('button', { name: 'Logout' }).click();
    await pg.waitForURL('**/Account/Login');
    await pause(500);
  }

  async function showTitle(title, subtitle = '') {
    await pg.evaluate(({ t, s }) => {
      const existing = document.getElementById('__demo_title__');
      if (existing) existing.remove();

      const el = document.createElement('div');
      el.id = '__demo_title__';
      el.style.cssText = [
        'position:fixed',
        'inset:0',
        'background:#14382b',
        'color:#fff',
        'display:flex',
        'flex-direction:column',
        'align-items:center',
        'justify-content:center',
        'z-index:99999',
        'font-family:system-ui,sans-serif',
        'pointer-events:none',
      ].join(';');

      el.innerHTML = `
        <div style="font-size:44px;font-weight:700;margin-bottom:14px;text-align:center;padding:0 60px;line-height:1.2">${t}</div>
        ${s ? `<div style="font-size:24px;opacity:0.8;text-align:center;padding:0 60px">${s}</div>` : ''}
      `;

      document.body.appendChild(el);
    }, { t: title, s: subtitle });

    await pause(subtitle ? 2200 : 2800);
    await pg.evaluate(() => document.getElementById('__demo_title__')?.remove());
    await pause(500);
  }

  async function fillApplication({
    email,
    firstName,
    lastName,
    dob,
    phone,
    address,
    postalCode,
    occupation,
    company,
    membershipLevel,
    asStaff,
  }) {
    await pg.goto(BASE + '/membership/apply');
    await blazorReady();

    await pg.getByRole('textbox', { name: 'Email Address' }).fill(email);
    await pg.getByRole('textbox', { name: 'First Name' }).fill(firstName);
    await pg.getByRole('textbox', { name: 'Last Name' }).fill(lastName);
    await pg.getByRole('textbox', { name: 'Date of Birth' }).fill(dob);
    await pg.getByRole('textbox', { name: 'Phone (e.g. (403) 555-1234)' }).fill(phone);
    await pg.getByRole('textbox', { name: 'Address' }).fill(address);
    await pg.getByRole('textbox', { name: 'Postal Code (e.g. T2A 4K3)' }).fill(postalCode);
    await pg.getByRole('textbox', { name: 'Occupation' }).fill(occupation);
    await pg.getByRole('textbox', { name: 'Company Name' }).fill(company);
    await pg.getByLabel('Membership Level').selectOption({ label: membershipLevel });

    if (asStaff) {
      await pg.locator('#sponsor1Search').fill('Alice');
      await pause(700);
      await pg.locator('button.list-group-item').filter({ hasText: /Alice/i }).first().click();

      await pg.locator('#sponsor2Search').fill('Bob');
      await pause(700);
      await pg.locator('button.list-group-item').filter({ hasText: /Bob/i }).first().click();

      await pg.locator('#endorsementConfirm').check();
    } else {
      await pg.getByRole('spinbutton', { name: 'Sponsor 1 Member ID' }).fill('1');
      await pg.getByRole('spinbutton', { name: 'Sponsor 2 Member ID' }).fill('2');
    }

    await pause(500);
    await pg.getByRole('button', { name: 'Submit Application' }).click();
    await waitForAlertText(/submitted|success|received/i);
    await pause(1600);
  }

  async function reviewAndApproveByLastName(lastName, level = 'Associate') {
    await pg.goto(BASE + '/membership/applications');
    await blazorReady();
    await pause(800);

    const targetRow = pg.locator('tbody tr').filter({ hasText: new RegExp(lastName, 'i') }).first();
    await targetRow.waitFor({ timeout: 15000 });
    await targetRow.getByRole('link', { name: 'Review' }).click();

    await pg.waitForURL('**/membership/applications/**');
    await blazorReady();

    await pg.getByLabel('New Status').selectOption('Accepted');
    await pause(500);
    await pg.getByLabel('Membership Level').selectOption({ label: level });
    await pg.getByRole('button', { name: 'Submit Decision' }).click();

    await waitForAlertText(/approved|accepted|created/i);
    await pause(1800);
  }

  async function bookFirstAvailableTeeTime(targetDateStr) {
    await pg.goto(BASE + '/teetimes');
    await blazorReady();

    await pg.locator('input[type="date"]').first().fill(targetDateStr);
    await pg.getByRole('button', { name: 'Refresh' }).click();
    await blazorReady();

    const dayBookable = pg.locator('.empty-slot-bookable').first();
    await dayBookable.waitFor({ timeout: 15000 });
    await dayBookable.click();

    await pg.waitForURL('**/teetimes/book**');
    await blazorReady();

    await pg.getByRole('button', { name: 'Book Tee Time' }).click();
    await waitForAlertText(/success|created|booked/i);
    await pause(1200);
  }

  async function showWeekViewForTwoWeeks(anchorDateStr) {
    await pg.goto(BASE + '/teetimes');
    await blazorReady();

    await pg.locator('input[type="date"]').first().fill(anchorDateStr);
    await pg.getByRole('button', { name: 'Refresh' }).click();
    await blazorReady();

    await pg.getByRole('button', { name: 'Week', exact: true }).click();
    await pause(1800);

    await pg.getByRole('button', { name: /Next/i }).click();
    await pause(1800);
  }

  // Intro
  addChapter('00 Intro', 'ClubBaist detailed demo overview and flow preview');
  await pg.goto(BASE + '/Account/Login');
  await pg.waitForLoadState('domcontentloaded');
  await showTitle('ClubBaist', 'Detailed End-to-End Demo');

  // Section 1: Membership
  addChapter('01 Membership Applications', 'Self-signup, admin-created application, committee approval, and account view');
  await showTitle('Memberships', 'Self-signup, admin-created application, committee approval, member account view');

  const runId = Date.now().toString().slice(-6);
  const selfFirst = 'Self';
  const selfLast = `Flow${runId}`;
  const selfEmail = `self.${runId}@example.com`;

  const adminFirst = 'Admin';
  const adminLast = `Flow${runId}`;
  const adminEmail = `admin.${runId}@example.com`;

  await login('shareholder3@clubbaist.com', 'Pass@word1');
  await fillApplication({
    email: selfEmail,
    firstName: selfFirst,
    lastName: selfLast,
    dob: '1990-05-15',
    phone: '(403) 555-1010',
    address: '101 Demo Lane',
    postalCode: 'T2A 4K3',
    occupation: 'Architect',
    company: 'Demo Corp',
    membershipLevel: 'Bronze',
    asStaff: false,
  });
  await logout();

  await login('admin@clubbaist.com', 'Pass@word1');
  await fillApplication({
    email: adminEmail,
    firstName: adminFirst,
    lastName: adminLast,
    dob: '1992-03-08',
    phone: '(403) 555-2020',
    address: '202 Admin Way',
    postalCode: 'T2B 3C4',
    occupation: 'Administrator',
    company: 'Club Baist',
    membershipLevel: 'Associate',
    asStaff: true,
  });
  await logout();

  await login('committee@clubbaist.com', 'Pass@word1');
  await reviewAndApproveByLastName(adminLast, 'Associate');
  await logout();

  await login(adminEmail, 'ChangeMe123!');
  await pg.goto(BASE + '/Account/Manage');
  await blazorReady();
  await pause(1800);
  await logout();

  // Section 2: Tee Times
  addChapter('02 Tee Time Booking', 'Lower-privileged member books and reviews reservation details');
  await showTitle('Tee Times', 'Booking, standing request, approval, and 2-week schedule proof');

  const teeDate = '2026-06-13';
  const standingStart = '2026-06-20';
  const standingEnd = '2026-08-29';

  await login('silver@clubbaist.com', 'Pass@word1');
  await bookFirstAvailableTeeTime(teeDate);
  await pg.goto(BASE + '/teetimes/my');
  await blazorReady();
  await pause(1400);
  await pg.getByRole('link', { name: 'View/Edit' }).first().click();
  await blazorReady();
  await pause(1200);
  await logout();

  await login('shareholder1@clubbaist.com', 'Pass@word1');
  addChapter('03 Standing Tee Time Request', 'Shareholder submits a recurring standing tee time request');
  await pg.goto(BASE + '/teetimes/standing/request');
  await blazorReady();

  await pg.getByLabel(/Day of Week/i).selectOption('Saturday');
  await pg.getByLabel(/Requested Time/i).fill('08:00');
  await pg.getByLabel(/Start Date/i).fill(standingStart);
  await pg.getByLabel(/End Date/i).fill(standingEnd);

  await pg.locator('#player-0').selectOption({ label: /SH-0002/i });
  await pg.locator('#player-1').selectOption({ label: /SH-0003/i });
  await pg.locator('#player-2').selectOption({ label: /SV-0004/i });

  await pg.getByRole('button', { name: 'Submit Request' }).click();
  await waitForAlertText(/pending review|submitted/i);
  await pause(1400);
  await logout();

  await login('admin@clubbaist.com', 'Pass@word1');
  addChapter('04 Standing Tee Time Approval', 'Admin approves request and generates recurring bookings');
  await pg.goto(BASE + '/admin/standing-teetimes');
  await blazorReady();

  const draftRow = pg.locator('tbody tr').filter({ hasText: /Alice Shareholder/i }).first();
  await draftRow.waitFor({ timeout: 15000 });
  await draftRow.getByRole('button', { name: 'Approve' }).click();

  await pg.locator('input[type="time"]').last().fill('08:00');
  await pg.getByRole('button', { name: 'Confirm' }).click();

  await waitForAlertText(/approved|generated/i);
  await pause(1800);

  addChapter('05 Weekly Schedule Proof', 'Week view across two weeks showing standing bookings were created');
  await showWeekViewForTwoWeeks(standingStart);
  await logout();

  // Section 3: Scores
  addChapter('06 Score Entry and Handicap', 'Record round scores and show calculated handicap output');
  await showTitle('Player Scores', 'Record score and show calculated handicap');

  await login('shareholder1@clubbaist.com', 'Pass@word1');
  await pg.goto(BASE + '/scores/my');
  await blazorReady();
  await pause(1200);

  const recordLinks = pg.getByRole('link', { name: 'Record Score' });
  const recordCount = await recordLinks.count();
  if (recordCount > 0) {
    await recordLinks.first().click();
  } else {
    // Fallback for environments where the eligible list is temporarily empty.
    await pg.goto(BASE + '/scores/record?bookingId=1&memberId=1');
  }

  await blazorReady();
  await pause(700);

  await pg.locator('#tee-White').click();
  await pause(400);

  const scoreInputs = pg.locator('input[type="number"]');
  const scoreInputCount = await scoreInputs.count();
  for (let i = 0; i < Math.min(scoreInputCount, 18); i += 1) {
    await scoreInputs.nth(i).fill('5');
  }

  await pause(400);
  await pg.getByRole('button', { name: 'Submit Round' }).click();
  await pg.waitForURL('**/scores/confirmation**');
  await blazorReady();

  await pg.getByText(/Current Handicap Index/i).waitFor({ timeout: 15000 });
  await pause(2200);
  await logout();

  // End card
  await pg.goto(BASE + '/Account/Login');
  await pg.waitForLoadState('domcontentloaded');
  await showTitle('ClubBaist', 'Demo complete');

  await ctx.close();
  const videoPath = await pg.video()?.path();
  const chapterPath = writeChapterFile();
  await browser.close();

  console.log('\n✅ Detailed recording complete.');
  console.log('Video saved to:', videoPath ?? VIDEOS_DIR);
  if (chapterPath) {
    console.log('Chapters saved to:', chapterPath);
  }
}

main().catch((err) => {
  console.error('\n❌ Detailed recording failed:', err.message);
  process.exit(1);
});
