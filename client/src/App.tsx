import { useEffect } from 'react';
import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import Header from './components/layout/Header';
import Footer from './components/layout/Footer';
import HomePage from './pages/HomePage';
import AboutPage from './pages/AboutPage';
import FleetPage from './pages/FleetPage';
import AircraftPage from './pages/AircraftPage';
import ServicesPage from './pages/ServicesPage';
import ContactPage from './pages/ContactPage';
import SearchPage from './pages/SearchPage';
import BookingPage from './pages/BookingPage';
import CharterPage from './pages/CharterPage';
import AuthPage from './pages/AuthPage';
import MyBookingsPage from './pages/MyBookingsPage';
import PaymentPage from './pages/PaymentPage';

/** A client-side route change does not reset scroll on its own. */
function ScrollToTop() {
  const { pathname } = useLocation();
  useEffect(() => window.scrollTo(0, 0), [pathname]);
  return null;
}

export default function App() {
  const { i18n } = useTranslation();

  // Keeps <html lang> in step with the active language on first paint and on
  // every switch, which affects screen-reader pronunciation and hyphenation.
  useEffect(() => {
    document.documentElement.lang = i18n.resolvedLanguage ?? 'fr';
  }, [i18n.resolvedLanguage]);

  return (
    <>
      <ScrollToTop />
      <Header />

      <main>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/qui-sommes-nous" element={<AboutPage />} />
          <Route path="/nos-avions" element={<FleetPage />} />
          <Route path="/nos-avions/:id" element={<AircraftPage />} />
          <Route path="/nos-services" element={<ServicesPage />} />
          <Route path="/contacts" element={<ContactPage />} />

          {/* Scheduled: the Goma SD360 network, sold as seats. */}
          <Route path="/reserver" element={<SearchPage />} />
          <Route path="/reserver/:id" element={<BookingPage />} />
          <Route path="/mes-reservations" element={<MyBookingsPage />} />
          <Route path="/paiement/:id" element={<PaymentPage />} />

          {/* Charter: everything else, quoted by a person. */}
          <Route path="/devis" element={<CharterPage />} />

          <Route path="/connexion" element={<AuthPage mode="signIn" />} />
          <Route path="/inscription" element={<AuthPage mode="signUp" />} />

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>

      <Footer />
    </>
  );
}
