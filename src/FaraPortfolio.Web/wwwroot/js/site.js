// サイト全体の演出（Blazor から独立して動く）
//  - スクロール量を CSS 変数 --progress に流す（上部の進捗バー）。スクロールするのは .site-scroll（ヘッダーの下の領域）
//  - .reveal 要素が画面に入ったら .is-visible を付ける（フェードイン）
//  - 軌道アイコンはタッチ中に止める／スマホのメニューを外側タップ・Esc で閉じる
//  - works#id で来たら該当の作品カードの枠を 1 回光らせる
(() => {
    const root = document.documentElement;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    // スクロールするのは、ヘッダーとお知らせの帯の下の領域（Layout/MainLayout.razor の .site-scroll）。ページが読み込まれる前は無いので、都度探す
    const scrollEl = () => document.querySelector('.site-scroll');

    let ticking = false;
    const update = () => {
        const el = scrollEl();
        const y = el ? el.scrollTop : window.scrollY;
        const max = el ? el.scrollHeight - el.clientHeight : root.scrollHeight - window.innerHeight;
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
    // scroll は泡立たないので、捕まえ（capture）で、どの要素のスクロールも受け取る
    document.addEventListener('scroll', onScroll, { passive: true, capture: true });
    window.addEventListener('resize', onScroll, { passive: true });
    update();

    // 画面に入ったかどうかは、スクロールする領域（.site-scroll）の中で見る。領域がまだ無いときは、作られてから作る
    let observer = null;
    const getObserver = () => {
        if (observer) return observer;
        const container = scrollEl();
        if (!container) return null;
        observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    observer.unobserve(entry.target);
                }
            }
        }, { root: container, threshold: 0.1, rootMargin: '0px 0px -40px 0px' });
        return observer;
    };

    const scan = (node) => {
        if (node.nodeType !== Node.ELEMENT_NODE) return;
        const obs = getObserver();
        if (!obs) return;
        if (node.matches('.reveal:not(.is-visible)')) obs.observe(node);
        node.querySelectorAll('.reveal:not(.is-visible)').forEach((el) => obs.observe(el));
    };
    // Blazor が描画・ページ遷移で追加した要素も自動で監視する
    new MutationObserver((mutations) => {
        for (const m of mutations) m.addedNodes.forEach(scan);
        onScroll();
    }).observe(document.body, { childList: true, subtree: true });
    scan(document.body);
    root.classList.add('js-ready');

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
        // 別のページに移ったら、スクロールする領域を先頭に戻す（#ジャンプの移動では戻さない。scrollToHash が合わせる）
        resetScroll() {
            if (location.hash) return;
            scrollEl()?.scrollTo({ top: 0, behavior: 'instant' });
        },
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
