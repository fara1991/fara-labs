// サイト全体の演出（Blazor から独立して動く）
//  - スクロール量を CSS 変数 --progress に流す（上部の進捗バー）
//  - .reveal 要素が画面に入ったら .is-visible を付ける（フェードイン）
//  - 軌道アイコンはタッチ中に止める／スマホのメニューを外側タップ・Esc で閉じる
//  - works#id で来たら該当の作品カードの枠を 1 回光らせる
(() => {
    const root = document.documentElement;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

    let ticking = false;
    const update = () => {
        const y = window.scrollY;
        const max = root.scrollHeight - window.innerHeight;
        root.style.setProperty('--progress', max > 0 ? (y / max).toFixed(4) : '0');
        root.classList.toggle('is-scrolled', y > 8);
        ticking = false;
    };
    const onScroll = () => {
        if (!ticking) {
            ticking = true;
            requestAnimationFrame(update);
        }
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onScroll, { passive: true });
    update();

    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            }
        }
    }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });

    const scan = (node) => {
        if (node.nodeType !== Node.ELEMENT_NODE) return;
        if (node.matches('.reveal:not(.is-visible)')) observer.observe(node);
        node.querySelectorAll('.reveal:not(.is-visible)').forEach((el) => observer.observe(el));
    };
    // Blazor が描画・ページ遷移で追加した要素も自動で監視する
    new MutationObserver((mutations) => {
        for (const m of mutations) m.addedNodes.forEach(scan);
        onScroll();
    }).observe(document.body, { childList: true, subtree: true });
    scan(document.body);
    root.classList.add('js-ready');

    // メンテナンスの帯の実際の高さを --notice-h に入れる（2 行に折り返しても、ページ内ジャンプの位置が帯の下に隠れない）。
    //  JS が動かないときは CSS の見積もり（1 行分）のまま
    const noticeResize = 'ResizeObserver' in window ? new ResizeObserver(() => syncNotice()) : null;
    let noticeWatched = null;
    const syncNotice = () => {
        const el = document.querySelector('.maintenance-notice');
        if (el !== noticeWatched) {
            if (noticeWatched) noticeResize?.unobserve(noticeWatched);
            noticeWatched = el;
            if (el) noticeResize?.observe(el);
        }
        if (el) root.style.setProperty('--notice-h', el.offsetHeight + 'px');
        else root.style.removeProperty('--notice-h');
    };
    // 帯は Blazor が後から描く（出たり消えたりする）ので、DOM の変化を見て付け替える
    new MutationObserver(syncNotice).observe(document.body, { childList: true, subtree: true });
    syncNotice();

    // 軌道アイコンはタッチしている間とその直後は止めて、狙いやすくする
    let touchTimer = 0;
    document.addEventListener('pointerdown', (e) => {
        if (e.pointerType !== 'touch' || !(e.target instanceof Element)) return;
        const visual = e.target.closest('.hero-visual');
        if (!visual) return;
        visual.classList.add('is-touching');
        clearTimeout(touchTimer);
        touchTimer = setTimeout(() => visual.classList.remove('is-touching'), 4000);
    }, { passive: true });

    // スマホのメニュー: 開いたら最初のリンクへフォーカス、外側タップ・Esc で閉じる
    const menuToggle = () => document.querySelector('.menu-toggle');
    const menuOpen = () => menuToggle()?.getAttribute('aria-expanded') === 'true';
    document.addEventListener('click', (e) => {
        if (!menuOpen() || !(e.target instanceof Element)) return;
        if (e.target.closest('.site-nav, .menu-toggle')) return;
        menuToggle().click();
    });
    document.addEventListener('keydown', (e) => {
        if (e.key !== 'Escape' || !menuOpen()) return;
        menuToggle().click();
        menuToggle().focus();
    });
    let wasOpen = false;
    new MutationObserver(() => {
        const open = menuOpen();
        if (open && !wasOpen) requestAnimationFrame(() => document.querySelector('.site-nav .nav-link')?.focus());
        wasOpen = open;
    }).observe(document.body, { subtree: true, attributes: true, attributeFilter: ['aria-expanded'] });

    // トップの軌道アイコンの説明（.satellite-tip）が画面の左右からはみ出すときは内側に寄せる
    //  説明の位置は transform のアニメーション途中だと正しく測れないので、アイコンの中心と説明の幅から計算する
    const placeTip = (e) => {
        const sat = e.target instanceof Element ? e.target.closest('.satellite') : null;
        const tip = sat?.querySelector('.satellite-tip');
        if (!tip) return;
        const rect = sat.getBoundingClientRect();
        const center = (rect.left + rect.right) / 2;
        const half = tip.getBoundingClientRect().width / 2;
        const margin = 12;
        const overRight = center + half > root.clientWidth - margin;
        sat.classList.toggle('tip-left', overRight);
        sat.classList.toggle('tip-right', !overRight && center - half < margin);
    };
    document.addEventListener('pointerover', placeTip, { passive: true });
    document.addEventListener('focusin', placeTip);

    window.faraSite = {
        scrollToHash() {
            const id = decodeURIComponent(location.hash.slice(1));
            if (!id) return;
            const el = document.getElementById(id);
            if (!el) return;
            el.scrollIntoView({ behavior: reduceMotion.matches ? 'auto' : 'smooth', block: 'start' });
            // どの作品に飛んだか分かるよう、枠を 1 回だけ光らせる
            el.classList.remove('is-target');
            void el.offsetWidth;
            el.classList.add('is-target');
            el.addEventListener('animationend', () => el.classList.remove('is-target'), { once: true });
        },
    };
})();
